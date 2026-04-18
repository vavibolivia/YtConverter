using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using YtConverter.App.Logging;

namespace YtConverter.App.Services;

public sealed class FfmpegProvisioner : IFfmpegProvisioner
{
    private const string DownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
    private static readonly SemaphoreSlim _ensureLock = new(1, 1);
    private readonly string _cacheDir;
    private readonly string _cachedExe;
    private readonly string _cachedProbe;
    private readonly HttpClient _http;

    public string CacheDir => _cacheDir;
    public string FfprobePath => _cachedProbe;

    public FfmpegProvisioner(HttpClient? http = null)
    {
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YtConverter", "ffmpeg");
        _cachedExe = Path.Combine(_cacheDir, "ffmpeg.exe");
        _cachedProbe = Path.Combine(_cacheDir, "ffprobe.exe");
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    public async Task<string> EnsureAsync(CancellationToken ct = default)
    {
        if (File.Exists(_cachedExe))
        {
            AppLogger.Instance.Info($"FFmpeg 캐시 사용: {_cachedExe}");
            return _cachedExe;
        }

        // I-14: 병렬 첫 기동 race 방지 — 전역 락으로 직렬화
        await _ensureLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // 락 진입 후 재확인 (앞 스레드가 이미 설치 완료)
            if (File.Exists(_cachedExe))
            {
                AppLogger.Instance.Info($"FFmpeg 캐시 사용: {_cachedExe}");
                return _cachedExe;
            }
            return await EnsureInternalAsync(ct).ConfigureAwait(false);
        }
        finally { _ensureLock.Release(); }
    }

    private async Task<string> EnsureInternalAsync(CancellationToken ct)
    {

        var appDirCandidate = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(appDirCandidate))
        {
            AppLogger.Instance.Info($"FFmpeg 앱 폴더 사용: {appDirCandidate}");
            return appDirCandidate;
        }

        var onPath = FindOnPath("ffmpeg.exe");
        if (onPath is not null)
        {
            AppLogger.Instance.Info($"FFmpeg PATH 사용: {onPath}");
            return onPath;
        }

        AppLogger.Instance.Info("FFmpeg 미설치 — 다운로드 시작 (gyan.dev essentials)");
        Directory.CreateDirectory(_cacheDir);
        var zipPath = Path.Combine(_cacheDir, "ffmpeg.zip");

        using (var response = await _http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            await using var fs = File.Create(zipPath);
            await response.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
        }

        AppLogger.Instance.Info($"다운로드 완료 — zip 크기 {new FileInfo(zipPath).Length / 1024 / 1024} MB");

        using (var archive = ZipFile.OpenRead(zipPath))
        {
            await ExtractBinAsync(archive, "ffmpeg.exe", _cachedExe, ct).ConfigureAwait(false);
            await ExtractBinAsync(archive, "ffprobe.exe", _cachedProbe, ct).ConfigureAwait(false);
        }

        try { File.Delete(zipPath); } catch { }

        AppLogger.Instance.Info($"FFmpeg 설치 완료: {_cachedExe}");
        return _cachedExe;
    }

    private static async Task ExtractBinAsync(ZipArchive archive, string name, string dest, CancellationToken ct)
    {
        var entry = archive.Entries.FirstOrDefault(e =>
            e.FullName.Replace('\\', '/').EndsWith("/bin/" + name, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            throw new InvalidOperationException($"zip 내에서 {name} 를 찾지 못했습니다.");
        await using var src = entry.Open();
        await using var dst = File.Create(dest);
        await src.CopyToAsync(dst, ct).ConfigureAwait(false);
    }

    private static string? FindOnPath(string fileName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar)) return null;
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            try
            {
                var candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }
}
