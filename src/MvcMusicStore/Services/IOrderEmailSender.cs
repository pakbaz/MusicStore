using MvcMusicStore.Models;

namespace MvcMusicStore.Services;

/// <summary>
/// Sends the order confirmation / receipt email after a successful checkout.
/// </summary>
public interface IOrderEmailSender
{
    Task SendOrderConfirmationAsync(Order order, CancellationToken cancellationToken = default);
}
