using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MvcMusicStore.Models
{
    public class GiftCard
    {
        public int GiftCardId { get; set; }

        [Required]
        [StringLength(40)]
        public string Code { get; set; } = string.Empty;

        [DataType(DataType.Currency)]
        public decimal InitialAmount { get; set; }

        [DataType(DataType.Currency)]
        public decimal Balance { get; set; }

        [StringLength(256)]
        public string? PurchaserUsername { get; set; }

        [StringLength(256)]
        public string? RecipientEmail { get; set; }

        [StringLength(160)]
        public string? RecipientName { get; set; }

        [StringLength(160)]
        public string? SenderName { get; set; }

        [StringLength(500)]
        public string? Message { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime CreatedDate { get; set; }

        public bool IsActive { get; set; } = true;

        public List<GiftCardTransaction> Transactions { get; set; } = new();
    }

    public class GiftCardTransaction
    {
        public int TransactionId { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime Date { get; set; }

        [StringLength(40)]
        public string Type { get; set; } = string.Empty;

        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [DataType(DataType.Currency)]
        public decimal BalanceAfter { get; set; }

        public int? OrderId { get; set; }

        [StringLength(256)]
        public string? Username { get; set; }

        [StringLength(200)]
        public string? Note { get; set; }
    }

    public static class GiftCardTransactionTypes
    {
        public const string Issued = "Issued";
        public const string Redeemed = "Redeemed";
    }
}
