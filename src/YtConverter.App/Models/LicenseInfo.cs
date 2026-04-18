using System;

namespace YtConverter.App.Models;

public sealed class LicenseInfo
{
    public UserTier Tier { get; set; } = UserTier.Free;
    public string? Key { get; set; }
    public string? LicensedTo { get; set; }
    public DateTime LastResetUtc { get; set; } = DateTime.UtcNow.Date;
    public int DailyConversions { get; set; }
    public int TotalConversions { get; set; }
}
