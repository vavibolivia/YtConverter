using System;
using System.IO;
using System.Text.Json;
using YtConverter.App.Logging;
using YtConverter.App.Models;

namespace YtConverter.App.Services;

public sealed class LicenseService
{
    private const int FreeDailyLimit = 3;
    private const int FreeMaxConcurrency = 1;
    private const int ProMaxConcurrency = 3;
    private const int AdminMaxConcurrency = 5;

    // 관리자 마스터 키 (저자 본인 용도). 평문 코드에 포함되어 있으나
    // 프리미엄 모델의 스포트라이트용 예시 구현이다.
    private const string AdminMasterKey = "SUKHO-ADMIN-2026-MASTER";
    private static readonly string[] AdminUsernames = { "jjsuk" };

    private readonly string _licensePath;
    private LicenseInfo _license;

    public event Action? LicenseChanged;

    public LicenseInfo License => _license;
    public UserTier Tier => _license.Tier;
    public int MaxConcurrency => _license.Tier switch
    {
        UserTier.Admin => AdminMaxConcurrency,
        UserTier.Pro => ProMaxConcurrency,
        _ => FreeMaxConcurrency
    };

    public int FreeDailyQuota => FreeDailyLimit;
    public int RemainingToday => _license.Tier == UserTier.Free
        ? Math.Max(0, FreeDailyLimit - _license.DailyConversions)
        : int.MaxValue;

    public string TierBadge => _license.Tier switch
    {
        UserTier.Admin => "👑 Admin (무제한)",
        UserTier.Pro => "💎 Pro (무제한)",
        _ => $"🆓 Free ({RemainingToday}/{FreeDailyLimit} 오늘 남음)"
    };

    public LicenseService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YtConverter");
        Directory.CreateDirectory(dir);
        _licensePath = Path.Combine(dir, "license.json");
        _license = Load();
        MaybeAutoAdmin();
        ResetIfNewDay();
        Save();
    }

    private LicenseInfo Load()
    {
        try
        {
            if (File.Exists(_licensePath))
                return JsonSerializer.Deserialize<LicenseInfo>(File.ReadAllText(_licensePath))
                       ?? new LicenseInfo();
        }
        catch { }
        return new LicenseInfo();
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_licensePath, JsonSerializer.Serialize(_license, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private void MaybeAutoAdmin()
    {
        if (_license.Tier == UserTier.Admin) return;
        var user = Environment.UserName;
        foreach (var admin in AdminUsernames)
        {
            if (string.Equals(user, admin, StringComparison.OrdinalIgnoreCase))
            {
                _license.Tier = UserTier.Admin;
                _license.LicensedTo = $"{user} (auto)";
                AppLogger.Instance.Info($"자동 관리자 승격: Windows 계정 '{user}'");
                return;
            }
        }
    }

    private void ResetIfNewDay()
    {
        var today = DateTime.UtcNow.Date;
        if (_license.LastResetUtc.Date != today)
        {
            _license.LastResetUtc = today;
            _license.DailyConversions = 0;
        }
    }

    public bool CanConvert(out string? reason)
    {
        ResetIfNewDay();
        if (_license.Tier != UserTier.Free) { reason = null; return true; }
        if (_license.DailyConversions < FreeDailyLimit) { reason = null; return true; }
        reason = $"무료 플랜은 하루 {FreeDailyLimit}건까지 변환할 수 있습니다. Pro 로 업그레이드하거나 내일 다시 시도하세요.";
        return false;
    }

    public void RecordConversion()
    {
        ResetIfNewDay();
        _license.TotalConversions++;
        if (_license.Tier == UserTier.Free) _license.DailyConversions++;
        Save();
        LicenseChanged?.Invoke();
    }

    public bool ApplyKey(string key, out string message)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            message = "키를 입력하세요.";
            return false;
        }
        key = key.Trim();
        if (string.Equals(key, AdminMasterKey, StringComparison.Ordinal))
        {
            _license.Tier = UserTier.Admin;
            _license.Key = key;
            _license.LicensedTo = Environment.UserName + " (master)";
            Save();
            LicenseChanged?.Invoke();
            message = "관리자 모드가 활성화되었습니다.";
            AppLogger.Instance.Info("관리자 모드 활성");
            return true;
        }
        if (key.StartsWith("PRO-", StringComparison.OrdinalIgnoreCase) && key.Length >= 10)
        {
            _license.Tier = UserTier.Pro;
            _license.Key = key;
            _license.LicensedTo = Environment.UserName;
            Save();
            LicenseChanged?.Invoke();
            message = "Pro 라이선스가 적용되었습니다. 무제한 변환을 이용하세요.";
            AppLogger.Instance.Info($"Pro 라이선스 적용: {key[..Math.Min(8, key.Length)]}…");
            return true;
        }
        message = "유효하지 않은 키입니다. 올바른 Pro/Admin 키를 입력하세요.";
        return false;
    }

    public string IssueMockProKey()
    {
        // 실제 결제 연동 전 데모용 키 발급
        var key = $"PRO-{Guid.NewGuid().ToString("N").ToUpperInvariant()[..12]}";
        ApplyKey(key, out _);
        return key;
    }
}
