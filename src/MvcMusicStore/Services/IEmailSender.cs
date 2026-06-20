namespace MvcMusicStore.Services;

/// <summary>Low-level transport that delivers a fully-rendered email.</summary>
public interface IEmailSender
{
    Task SendAsync(OutgoingEmail message, CancellationToken cancellationToken = default);
}
