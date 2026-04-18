using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos.Streams;
using YtConverter.App.Logging;
using YtConverter.App.Models;

namespace YtConverter.App.Services;

public sealed class DownloadService : IDownloadService
{
    private readonly YoutubeClient _yt;
    private readonly IFfmpegProvisioner _ffmpeg;
    private readonly HttpClient _http;
    private readonly string _workRoot;

    private const long ChunkSize = 9_898_989; // ~9.4 MB — YoutubeExplode 가 사용하는 값

    public DownloadService(IFfmpegProvisioner ffmpeg)
    {
        _yt = new YoutubeClient();
        _ffmpeg = ffmpeg;
        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        // YouTube 는 일반 User-Agent 없으면 단일 연결을 매우 느리게 throttle 함
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.AcceptEncoding.ParseAdd("identity");
        _workRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YtConverter", "work");
        Directory.CreateDirectory(_workRoot);
    }

    public async Task<ConversionResult> ConvertAsync(
        string url,
        OutputFormat format,
        string outputFolder,
        IProgress<ConversionProgress> progress,
        CancellationToken ct)
    {
        progress.Report(new ConversionProgress(JobStatus.Resolving, 0, null));
        AppLogger.Instance.Info($"변환 시작: url={url}, format={format}, out={outputFolder}");

        var ffmpegPath = await _ffmpeg.EnsureAsync(ct).ConfigureAwait(false);

        YoutubeExplode.Videos.Video video;
        try
        {
            video = await _yt.Videos.GetAsync(url, ct).ConfigureAwait(false);
        }
        catch (Exception ex) { throw MapException(ex); }

        if (video.Duration is null)
            throw new InvalidOperationException("라이브 스트림은 변환할 수 없습니다.");

        progress.Report(new ConversionProgress(JobStatus.Resolving, 0.05, video.Title));
        AppLogger.Instance.Info($"영상 확인: \"{video.Title}\" ({video.Duration})");

        Directory.CreateDirectory(outputFolder);
        var safeTitle = SanitizeFileName(video.Title);
        var ext = format == OutputFormat.Mp3 ? "mp3" : "mp4";
        var outputPath = Path.Combine(outputFolder, $"{safeTitle}.{ext}");

        // 이미 변환된 결과가 있으면 재사용 (번호 붙이기 지양)
        if (File.Exists(outputPath))
        {
            var fi = new FileInfo(outputPath);
            if (fi.Length > 1024) // 유의미한 크기 = 이전 변환 성공물
            {
                progress.Report(new ConversionProgress(JobStatus.Completed, 1.0, video.Title));
                AppLogger.Instance.Info($"기존 파일 재사용 (이미 변환됨): {outputPath}");
                return new ConversionResult(outputPath, video.Title, video.Duration ?? TimeSpan.Zero);
            }
            // 0바이트 placeholder 는 삭제
            try { File.Delete(outputPath); } catch { }
        }

        // 새 placeholder 예약 (동시 변환 충돌 방지용, 실패 시 finally 에서 정리)
        try
        {
            using var fs = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (IOException)
        {
            // 바로 직전에 다른 스레드가 만들었을 수 있음 → GUID suffix 로 고유화
            outputPath = Path.Combine(outputFolder, $"{safeTitle}_{Guid.NewGuid().ToString("N")[..6]}.{ext}");
            using var fs = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        bool placeholderStillReserved = true;

        // 재개 가능한 작업 디렉터리 (URL+format 이 같으면 같은 디렉터리 재사용)
        // B-02 수정: URL 전체 대신 videoId 로 hash — &t=, &list= 등 쿼리가 달라도 같은 work 공유
        var jobId = StableJobId(video.Id.Value, format);
        var workDir = Path.Combine(_workRoot, jobId);
        Directory.CreateDirectory(workDir);

        StreamManifest manifest;
        try
        {
            manifest = await _yt.Videos.Streams.GetManifestAsync(url, ct).ConfigureAwait(false);
        }
        catch (Exception ex) { throw MapException(ex); }

        // 스트림 선택
        List<IStreamInfo> chosen;
        if (format == OutputFormat.Mp3)
        {
            var audio = manifest.GetAudioOnlyStreams().GetWithHighestBitrate()
                ?? throw new InvalidOperationException("오디오 스트림을 찾지 못했습니다.");
            chosen = new List<IStreamInfo> { audio };
        }
        else
        {
            var videoStream = manifest.GetVideoOnlyStreams()
                .Where(s => s.Container == Container.Mp4)
                .OrderByDescending(s => s.VideoQuality)
                .FirstOrDefault()
                ?? manifest.GetVideoOnlyStreams().GetWithHighestVideoQuality()
                ?? throw new InvalidOperationException("비디오 스트림을 찾지 못했습니다.");
            var audio = manifest.GetAudioOnlyStreams().GetWithHighestBitrate()
                ?? throw new InvalidOperationException("오디오 스트림을 찾지 못했습니다.");
            chosen = new List<IStreamInfo> { videoStream, audio };
        }

        try
        {
            // 스트림 다운로드 (Range resume)
            long totalBytes = chosen.Sum(s => s.Size.Bytes);
            long cumulative = chosen.Sum(s => FileSize(Path.Combine(workDir, StreamFileName(s))));
            var streamPaths = new List<string>();
            try
            {
                foreach (var s in chosen)
                {
                    var streamPath = Path.Combine(workDir, StreamFileName(s));
                    streamPaths.Add(streamPath);
                    await DownloadStreamWithResumeAsync(s, streamPath, p =>
                    {
                        var overall = totalBytes > 0 ? 0.1 + 0.8 * (cumulative + p) / totalBytes : 0.1;
                        progress.Report(new ConversionProgress(JobStatus.Downloading, Math.Clamp(overall, 0, 1), video.Title));
                    }, ct).ConfigureAwait(false);
                    cumulative += s.Size.Bytes;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                AppLogger.Instance.Error("스트림 다운로드 실패", ex);
                throw MapException(ex);
            }

            progress.Report(new ConversionProgress(JobStatus.Muxing, 0.95, video.Title));

            var partPath = outputPath + ".part";
            try { if (File.Exists(partPath)) File.Delete(partPath); } catch { }

            try
            {
                await RunFfmpegAsync(ffmpegPath, streamPaths, partPath, format, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try { if (File.Exists(partPath)) File.Delete(partPath); } catch { }
                throw;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(partPath)) File.Delete(partPath); } catch { }
                AppLogger.Instance.Error("FFmpeg mux 실패", ex);
                throw MapException(ex);
            }

            // 성공 → 최종 파일로 교체 (placeholder 덮어쓰기)
            try
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
                File.Move(partPath, outputPath);
                placeholderStillReserved = false;
            }
            catch (Exception ex)
            {
                AppLogger.Instance.Error("최종 파일 이동 실패", ex);
                throw;
            }

            try { Directory.Delete(workDir, true); } catch { }

            progress.Report(new ConversionProgress(JobStatus.Completed, 1.0, video.Title));
            AppLogger.Instance.Info($"변환 완료: {outputPath}");
            return new ConversionResult(outputPath, video.Title, video.Duration ?? TimeSpan.Zero);
        }
        finally
        {
            // 실패/취소 시 0바이트 placeholder 가 사용자 폴더에 남지 않도록
            if (placeholderStillReserved)
            {
                try
                {
                    if (File.Exists(outputPath) && new FileInfo(outputPath).Length == 0)
                    {
                        File.Delete(outputPath);
                        AppLogger.Instance.Info($"placeholder 정리: {Path.GetFileName(outputPath)}");
                    }
                }
                catch { }
            }
        }
    }

    private async Task DownloadStreamWithResumeAsync(
        IStreamInfo info, string destPath, Action<long> progressBytes, CancellationToken ct)
    {
        long expected = info.Size.Bytes;
        long existing = FileSize(destPath);
        if (existing > expected)
        {
            try { File.Delete(destPath); } catch { }
            existing = 0;
        }
        if (existing == expected && expected > 0)
        {
            AppLogger.Instance.Info($"스트림 재사용: {Path.GetFileName(destPath)} ({existing:N0} B)");
            progressBytes(existing);
            return;
        }

        AppLogger.Instance.Info(existing > 0
            ? $"스트림 이어받기: {Path.GetFileName(destPath)} ({existing:N0} / {expected:N0} B)"
            : $"스트림 다운로드: {Path.GetFileName(destPath)} ({expected:N0} B)");

        // YouTube 의 throttling 회피: 큰 파일을 ~9.4MB 청크로 Range 요청
        // (YoutubeExplode 와 동일한 전략)
        await using var output = new FileStream(destPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        output.Seek(existing, SeekOrigin.Begin);
        output.SetLength(existing);

        long position = existing;
        while (position < expected)
        {
            ct.ThrowIfCancellationRequested();
            long chunkEnd = Math.Min(position + ChunkSize - 1, expected - 1);

            var req = new HttpRequestMessage(HttpMethod.Get, info.Url);
            req.Headers.Range = new RangeHeaderValue(position, chunkEnd);

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (resp.StatusCode != HttpStatusCode.PartialContent && resp.StatusCode != HttpStatusCode.OK)
            {
                resp.EnsureSuccessStatusCode();
            }

            await using var input = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var buf = new byte[81920];
            int n;
            while ((n = await input.ReadAsync(buf, ct).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buf.AsMemory(0, n), ct).ConfigureAwait(false);
                position += n;
                progressBytes(position);
            }
        }
        await output.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task RunFfmpegAsync(
        string ffmpegPath, List<string> inputs, string output, OutputFormat format, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.Append("-y ");
        foreach (var i in inputs) sb.Append($"-i \"{i}\" ");
        if (format == OutputFormat.Mp3)
            sb.Append("-vn -c:a libmp3lame -q:a 2 -f mp3 ");  // VBR ~190 kbps, 출력 확장자 무관하게 mp3 강제
        else
            sb.Append("-map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -b:a 192k -f mp4 ");
        sb.Append($"\"{output}\"");

        var args = sb.ToString();
        AppLogger.Instance.Info($"FFmpeg args: {args}");
        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.Start();
        var errTask = proc.StandardError.ReadToEndAsync();
        var outTask = proc.StandardOutput.ReadToEndAsync();

        using (ct.Register(() =>
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
        }))
        {
            await proc.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        ct.ThrowIfCancellationRequested();

        if (proc.ExitCode != 0)
        {
            var err = await errTask.ConfigureAwait(false);
            // stderr 마지막 2000자 (에러는 보통 마지막에 나타남)
            var tail = err.Length > 2000 ? err[^2000..] : err;
            AppLogger.Instance.Error($"FFmpeg exit={proc.ExitCode} stderr tail:\n{tail}");
            throw new InvalidOperationException($"FFmpeg 실패 (exit={proc.ExitCode}). 자세한 내용은 로그 참고.");
        }
    }

    private static string StreamFileName(IStreamInfo info)
    {
        var ext = info.Container.Name; // mp4, webm, m4a, etc.
        var kind = info switch
        {
            AudioOnlyStreamInfo => "audio",
            VideoOnlyStreamInfo => "video",
            _ => "muxed"
        };
        return $"{kind}.{ext}";
    }

    private static string StableJobId(string videoIdOrKey, OutputFormat fmt)
    {
        using var sha = SHA1.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes($"{videoIdOrKey}|{fmt}"));
        return Convert.ToHexString(bytes)[..16];
    }

    // B-10: 사용자 출력 폴더의 앱 생성 임시 파일 청소
    public static void CleanupStaleArtifacts(string outputFolder)
    {
        try
        {
            if (!Directory.Exists(outputFolder)) return;
            foreach (var f in Directory.EnumerateFiles(outputFolder))
            {
                try
                {
                    var name = Path.GetFileName(f);
                    if (name.EndsWith(".part", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains(".mp3.stream-", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains(".mp4.stream-", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".ytdl-tmp", StringComparison.OrdinalIgnoreCase))
                    {
                        var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(f);
                        if (age.TotalHours > 1) // 1시간 이상 방치 = 고아
                        {
                            File.Delete(f);
                            AppLogger.Instance.Info($"고아 임시파일 정리: {name}");
                        }
                    }
                    else if (new FileInfo(f).Length == 0)
                    {
                        // 0바이트 placeholder 는 즉시 삭제
                        File.Delete(f);
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private static long FileSize(string path) =>
        File.Exists(path) ? new FileInfo(path).Length : 0;

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max]);

    private static Exception MapException(Exception ex) => ex switch
    {
        VideoUnavailableException => new InvalidOperationException("삭제되었거나 비공개 영상입니다.", ex),
        VideoUnplayableException => new InvalidOperationException("재생 불가 영상입니다 (연령/지역 제한).", ex),
        HttpRequestException => new InvalidOperationException("네트워크 오류. 다시 시도하세요.", ex),
        // B-03: UnauthorizedAccessException 도 파일 쓰기 실패로 분류
        UnauthorizedAccessException => new InvalidOperationException("저장 폴더에 쓰기 권한이 없습니다.", ex),
        IOException => new InvalidOperationException("저장 공간이 부족하거나 파일을 쓸 수 없습니다.", ex),
        _ => ex
    };

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        if (cleaned.Length > 120) cleaned = cleaned[..120];
        return string.IsNullOrWhiteSpace(cleaned) ? "output" : cleaned;
    }

}
