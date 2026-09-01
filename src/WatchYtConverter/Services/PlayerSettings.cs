using System.IO;
using System.Text.Json;
using YtConverter.App.Logging;

namespace WatchYtConverter.Services;

/// <summary>
/// 플레이어 설정. %LOCALAPPDATA%\YtConverter\player-settings.json 에 보관한다.
/// 설정을 읽지 못해도 앱은 기본값으로 계속 동작해야 하므로 모든 실패는 삼킨다.
/// </summary>
public sealed class PlayerSettings
{
    public string? OutputFolder { get; set; }
    public double Volume { get; set; } = 0.8;
    /// <summary>썸네일 클릭 즉시 변환. 기본은 꺼짐(유튜브처럼 영상 재생).</summary>
    public bool InstantConvertEnabled { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YtConverter", "player-settings.json");

    public static string DefaultOutputFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "YtConverter");

    public static PlayerSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<PlayerSettings>(File.ReadAllText(FilePath));
                if (s is not null)
                {
                    if (string.IsNullOrWhiteSpace(s.OutputFolder)) s.OutputFolder = DefaultOutputFolder;
                    return s;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Instance.Warn($"[Player] 설정 읽기 실패: {ex.Message}");
        }
        return new PlayerSettings { OutputFolder = DefaultOutputFolder };
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            AppLogger.Instance.Warn($"[Player] 설정 저장 실패: {ex.Message}");
        }
    }
}
