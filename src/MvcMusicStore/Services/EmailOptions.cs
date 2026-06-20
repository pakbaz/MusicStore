namespace MvcMusicStore.Services;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Master switch. When false, no email is dispatched (sending is skipped and logged).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Delivery provider: "Log" (default, writes to logs/files) or "Acs" (Azure Communication Services).</summary>
    public string Provider { get; set; } = "Log";

    /// <summary>The verified sender address, e.g. "DoNotReply@your-domain.azurecomm.net".</summary>
    public string FromAddress { get; set; } = "DoNotReply@localhost";

    /// <summary>Friendly sender display name.</summary>
    public string FromName { get; set; } = "MVC Music Store";

    /// <summary>ACS connection string (local/dev or secret-backed). Takes precedence over <see cref="Endpoint"/>.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>ACS resource endpoint used with managed identity when no connection string is provided.</summary>
    public string? Endpoint { get; set; }

    /// <summary>Public base URL of the site, used to build absolute links (unsubscribe, cart, orders).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Optional directory for the log/file sender to persist rendered emails (.html) during development.</summary>
    public string? LogDirectory { get; set; }

    public bool UsesAcs => string.Equals(Provider, "Acs", StringComparison.OrdinalIgnoreCase);
}
