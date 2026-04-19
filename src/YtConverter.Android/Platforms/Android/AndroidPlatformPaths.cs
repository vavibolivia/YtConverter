using YtConverter.Shared.Services.Abstractions;
using AndroidApp = Android.App.Application;
using AndroidEnv = Android.OS.Environment;

namespace YtConverter.Droid.Platforms.Android;

/// <summary>
/// Android 용 IPlatformPaths 구현.
/// 우선순위: **앱 전용 외부 Music** (/Android/data/&lt;pkg&gt;/files/Music/YtConverter).
/// - 권한 불필요, Scoped Storage(API29+) 영향 없음, MTP/파일관리자 접근 가능.
/// - 공용 /storage/emulated/0/Music 은 API29+ 에서 MediaStore API 필수 → Phase D2 에서 재구현.
/// </summary>
public sealed class AndroidPlatformPaths : IPlatformPaths
{
    public AndroidPlatformPaths()
    {
        var ctx = AndroidApp.Context;

        // --- MusicDirectory: 앱 전용 외부 Music (/Android/data/<pkg>/files/Music/YtConverter) ---
        // GetExternalFilesDir(Music) 는 권한 없이 항상 쓰기 가능. 실패하면 내부 files/Music 로 폴백.
        var musicDir = System.IO.Path.Combine(
            ctx.GetExternalFilesDir(AndroidEnv.DirectoryMusic)?.AbsolutePath
                ?? System.IO.Path.Combine(ctx.FilesDir?.AbsolutePath ?? "/data/local/tmp", "Music"),
            "YtConverter");

        // --- CacheDirectory: 내부 cache/work ---
        var cacheRoot = ctx.CacheDir?.AbsolutePath ?? "/data/local/tmp/cache";

        // --- LogsDirectory: app external files/logs (adb pull 용이) ---
        var filesExternal = ctx.GetExternalFilesDir(null)?.AbsolutePath
                            ?? ctx.FilesDir?.AbsolutePath
                            ?? "/data/local/tmp";

        // --- Settings/Queue: app internal files (재설치/지우기 전까지 유지) ---
        var filesInternal = ctx.FilesDir?.AbsolutePath ?? "/data/local/tmp";

        MusicDirectory   = EnsureDir(musicDir);
        CacheDirectory   = EnsureDir(System.IO.Path.Combine(cacheRoot, "work"));
        LogsDirectory    = EnsureDir(System.IO.Path.Combine(filesExternal, "logs"));
        SettingsFilePath = System.IO.Path.Combine(filesInternal, "settings.json");
        QueueFilePath    = System.IO.Path.Combine(filesInternal, "queue.json");
    }

    public string MusicDirectory { get; }
    public string CacheDirectory { get; }
    public string LogsDirectory { get; }
    public string SettingsFilePath { get; }
    public string QueueFilePath { get; }

    private static string EnsureDir(string path)
    {
        try { System.IO.Directory.CreateDirectory(path); } catch { /* best-effort */ }
        return path;
    }
}
