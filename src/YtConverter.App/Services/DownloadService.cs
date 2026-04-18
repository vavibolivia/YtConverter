using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Converter;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Videos.Streams;
using YtConverter.App.Logging;
using YtConverter.App.Models;

namespace YtConverter.App.Services;

public sealed class DownloadService : IDownloadService
{
    private readonly YoutubeClient _yt;
    private readonly IFfmpegProvisioner _ffmpeg;

    public DownloadService(IFfmpegProvisioner ffmpeg)
    {
        _yt = new YoutubeClient();
        _ffmpeg = ffmpeg;
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

        var video = await _yt.Videos.GetAsync(url, ct).ConfigureAwait(false);
        if (video.Duration is null)
            throw new InvalidOperationException("라이브 스트림은 변환할 수 없습니다.");

        progress.Report(new ConversionProgress(JobStatus.Resolving, 0.05, video.Title));
        AppLogger.Instance.Info($"영상 확인: \"{video.Title}\" ({video.Duration})");

        Directory.CreateDirectory(outputFolder);
        var safeTitle = SanitizeFileName(video.Title);
        var ext = format == OutputFormat.Mp3 ? "mp3" : "mp4";
        var outputPath = GetUniquePath(Path.Combine(outputFolder, $"{safeTitle}.{ext}"));

        var manifest = await _yt.Videos.Streams.GetManifestAsync(url, ct).ConfigureAwait(false);
        IStreamInfo[] streams;
        Container container;

        if (format == OutputFormat.Mp3)
        {
            var audio = manifest.GetAudioOnlyStreams().GetWithHighestBitrate()
                ?? throw new InvalidOperationException("오디오 스트림을 찾지 못했습니다.");
            streams = new IStreamInfo[] { audio };
            container = Container.Mp3;
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
            streams = new IStreamInfo[] { videoStream, audio };
            container = Container.Mp4;
        }

        progress.Report(new ConversionProgress(JobStatus.Downloading, 0.1, video.Title));

        var innerProgress = new Progress<double>(ratio =>
        {
            // 10% resolving + 85% download/mux + 5% finalize
            var overall = 0.1 + ratio * 0.85;
            progress.Report(new ConversionProgress(
                ratio >= 0.99 ? JobStatus.Muxing : JobStatus.Downloading,
                Math.Clamp(overall, 0, 1),
                video.Title));
        });

        try
        {
            var request = new ConversionRequestBuilder(outputPath)
                .SetContainer(container)
                .SetFFmpegPath(ffmpegPath)
                .SetPreset(ConversionPreset.Medium)
                .Build();

            await _yt.Videos.DownloadAsync(streams, request, innerProgress, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryDelete(outputPath);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(outputPath);
            AppLogger.Instance.Error("변환 실패", ex);
            throw MapException(ex);
        }

        progress.Report(new ConversionProgress(JobStatus.Completed, 1.0, video.Title));
        AppLogger.Instance.Info($"변환 완료: {outputPath}");
        return new ConversionResult(outputPath, video.Title, video.Duration ?? TimeSpan.Zero);
    }

    private static Exception MapException(Exception ex) => ex switch
    {
        VideoUnavailableException => new InvalidOperationException("삭제되었거나 비공개 영상입니다.", ex),
        VideoUnplayableException => new InvalidOperationException("재생 불가 영상입니다 (연령/지역 제한).", ex),
        System.Net.Http.HttpRequestException => new InvalidOperationException("네트워크 오류. 다시 시도하세요.", ex),
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

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
        return path;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
