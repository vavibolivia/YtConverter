using System;
using System.Collections.Concurrent;
using System.IO;

namespace YtConverter.App.Logging;

public sealed class AppLogger
{
    private static readonly Lazy<AppLogger> _instance = new(() => new AppLogger());
    public static AppLogger Instance => _instance.Value;

    private readonly string _logDir;
    private readonly object _fileLock = new();
    private readonly ConcurrentQueue<Action<string>> _sinks = new();

    private AppLogger()
    {
        _logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YtConverter", "logs");
        // I-15: Controlled Folder Access / 권한 없음 시 앱 크래시 방지
        try { Directory.CreateDirectory(_logDir); } catch { /* 파일 로그 비활성, UI sink 만 동작 */ }
    }

    public void AttachSink(Action<string> sink) => _sinks.Enqueue(sink);

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message} :: {ex.GetType().Name}: {ex.Message}");

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level,-5} {message}";
        var filePath = Path.Combine(_logDir, $"yt-{DateTime.Now:yyyyMMdd}.log");
        try
        {
            lock (_fileLock)
            {
                File.AppendAllText(filePath, line + Environment.NewLine);
            }
        }
        catch { /* best-effort */ }

        foreach (var sink in _sinks)
        {
            try { sink(line); } catch { /* swallow UI sink errors */ }
        }
    }
}
