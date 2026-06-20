namespace MvcMusicStore.Services;

/// <summary>A provider-agnostic email ready for dispatch by an <see cref="IEmailSender"/>.</summary>
public sealed record OutgoingEmail
{
    public required string ToAddress { get; init; }
    public string? ToName { get; init; }
    public required string Subject { get; init; }
    public required string HtmlBody { get; init; }
    public required string PlainTextBody { get; init; }
}
