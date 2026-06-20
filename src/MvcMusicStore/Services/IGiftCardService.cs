using System;
using System.Threading;
using System.Threading.Tasks;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services
{
    public interface IGiftCardService
    {
        // Generates a new unguessable, human-readable gift-card code.
        string GenerateCode();

        // Normalizes user input (trim + uppercase) so codes match regardless of casing/whitespace.
        string NormalizeCode(string? code);

        // Returns the active gift card matching the code (tracked by the context), or null.
        Task<GiftCard?> GetActiveByCodeAsync(string? code, CancellationToken cancellationToken = default);

        // Creates, persists, and returns a new gift card with an opening "Issued" transaction.
        Task<GiftCard> IssueAsync(
            decimal amount,
            string? purchaserUsername,
            string recipientEmail,
            string? recipientName,
            string? senderName,
            string? message,
            CancellationToken cancellationToken = default);

        // Applies up to the requested amount against the card's balance and records a redemption
        // transaction on the (already tracked) card. Returns the amount actually applied.
        // The caller is responsible for SaveChangesAsync so checkout can persist atomically.
        decimal Redeem(GiftCard card, decimal amountRequested, int? orderId, string? username);
    }
}
