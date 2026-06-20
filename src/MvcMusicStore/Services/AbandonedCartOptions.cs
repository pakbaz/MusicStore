namespace MvcMusicStore.Services;

public sealed class AbandonedCartOptions
{
    public const string SectionName = "AbandonedCart";

    /// <summary>Enables the abandoned-cart reminder background worker.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Delay before the worker's first scan after startup.</summary>
    public int StartupDelaySeconds { get; set; } = 30;

    /// <summary>How often the worker scans for abandoned carts.</summary>
    public int ScanIntervalMinutes { get; set; } = 15;

    /// <summary>A cart is "abandoned" once its most recent item has gone untouched for this long.</summary>
    public int ReminderAfterMinutes { get; set; } = 60;

    /// <summary>Minimum time before the same (unchanged) cart can be reminded again.</summary>
    public int ResendAfterHours { get; set; } = 72;

    /// <summary>Optional incentive/promo code surfaced in the reminder email.</summary>
    public string? IncentiveCode { get; set; }

    /// <summary>Maximum number of reminders dispatched per scan pass.</summary>
    public int BatchSize { get; set; } = 50;
}
