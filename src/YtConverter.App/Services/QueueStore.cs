using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using YtConverter.App.Logging;
using YtConverter.App.Models;

namespace YtConverter.App.Services;

public sealed class QueueStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
    private readonly object _saveLock = new();

    public QueueStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YtConverter");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "queue.json");
    }

    public List<JobSnapshot> Load()
    {
        try
        {
            // B-07: 이전 세션의 .tmp 파편 정리
            var tmp = _path + ".tmp";
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }

            if (!File.Exists(_path)) return new();
            var json = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(json)) return new();
            return JsonSerializer.Deserialize<List<JobSnapshot>>(json, JsonOpts) ?? new();
        }
        catch (Exception ex)
        {
            AppLogger.Instance.Warn($"큐 로드 실패 (무시): {ex.Message}");
            return new();
        }
    }

    public void Save(IEnumerable<JobSnapshot> snapshots)
    {
        lock (_saveLock)
        {
            try
            {
                var tmp = _path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(snapshots, JsonOpts));
                // 원자적 교체
                if (File.Exists(_path)) File.Replace(tmp, _path, null);
                else File.Move(tmp, _path);
            }
            catch (Exception ex)
            {
                AppLogger.Instance.Warn($"큐 저장 실패: {ex.Message}");
            }
        }
    }
}
