using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services
{
    public class GiftCardService : IGiftCardService
    {
        private readonly MusicStoreEntities _db;

        // Unambiguous alphabet (no 0/O/1/I) so printed codes are easy to read and type.
        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const string CodePrefix = "GIFT";

        public GiftCardService(MusicStoreEntities db)
        {
            _db = db;
        }

        public string GenerateCode()
        {
            var builder = new StringBuilder(CodePrefix);
            for (var group = 0; group < 3; group++)
            {
                builder.Append('-');
                for (var i = 0; i < 4; i++)
                {
                    builder.Append(CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]);
                }
            }

            return builder.ToString();
        }

        public string NormalizeCode(string? code)
        {
            return (code ?? string.Empty).Trim().ToUpperInvariant();
        }

        public async Task<GiftCard?> GetActiveByCodeAsync(string? code, CancellationToken cancellationToken = default)
        {
            var normalized = NormalizeCode(code);
            if (string.IsNullOrEmpty(normalized))
            {
                return null;
            }

            // Cosmos cannot translate AnyAsync()/EXISTS subqueries, so materialize the match.
            var matches = await _db.GiftCards
                .Where(g => g.Code == normalized)
                .Take(1)
                .ToListAsync(cancellationToken);

            var card = matches.FirstOrDefault();
            return card is { IsActive: true } ? card : null;
        }

        public async Task<GiftCard> IssueAsync(
            decimal amount,
            string? purchaserUsername,
            string recipientEmail,
            string? recipientName,
            string? senderName,
            string? message,
            CancellationToken cancellationToken = default)
        {
            var createdDate = DateTime.Now;
            var card = new GiftCard
            {
                GiftCardId = await _db.NextGiftCardIdAsync(cancellationToken),
                Code = await GenerateUniqueCodeAsync(cancellationToken),
                InitialAmount = amount,
                Balance = amount,
                PurchaserUsername = purchaserUsername,
                RecipientEmail = recipientEmail,
                RecipientName = recipientName,
                SenderName = senderName,
                Message = message,
                CreatedDate = createdDate,
                IsActive = true
            };

            card.Transactions.Add(new GiftCardTransaction
            {
                TransactionId = 1,
                Date = createdDate,
                Type = GiftCardTransactionTypes.Issued,
                Amount = amount,
                BalanceAfter = amount,
                Username = purchaserUsername,
                Note = "Gift card issued"
            });

            _db.GiftCards.Add(card);
            await _db.SaveChangesAsync(cancellationToken);
            return card;
        }

        public decimal Redeem(GiftCard card, decimal amountRequested, int? orderId, string? username)
        {
            if (card is null)
            {
                throw new ArgumentNullException(nameof(card));
            }

            var applied = Math.Min(card.Balance, amountRequested);
            if (applied <= 0)
            {
                return 0m;
            }

            card.Balance -= applied;
            card.Transactions.Add(new GiftCardTransaction
            {
                TransactionId = NextTransactionId(card),
                Date = DateTime.Now,
                Type = GiftCardTransactionTypes.Redeemed,
                Amount = -applied,
                BalanceAfter = card.Balance,
                OrderId = orderId,
                Username = username,
                Note = orderId.HasValue ? $"Applied to order {orderId.Value}" : "Redeemed"
            });

            return applied;
        }

        private static int NextTransactionId(GiftCard card)
        {
            return card.Transactions.Count == 0 ? 1 : card.Transactions.Max(t => t.TransactionId) + 1;
        }

        private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var code = GenerateCode();
                var clash = (await _db.GiftCards
                    .Where(g => g.Code == code)
                    .Select(g => g.GiftCardId)
                    .Take(1)
                    .ToListAsync(cancellationToken)).Count != 0;

                if (!clash)
                {
                    return code;
                }
            }

            // Astronomically unlikely; fall back to a code with extra entropy.
            return $"{GenerateCode()}-{RandomNumberGenerator.GetInt32(1000, 9999)}";
        }
    }
}
