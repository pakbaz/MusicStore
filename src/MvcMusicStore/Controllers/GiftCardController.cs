using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.Services;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.Controllers
{
    [Authorize]
    public class GiftCardController : Controller
    {
        private readonly MusicStoreEntities storeDB;
        private readonly IGiftCardService giftCards;
        private readonly IEmailSender emailSender;

        public GiftCardController(
            MusicStoreEntities storeDb,
            IGiftCardService giftCardService,
            IEmailSender emailSender)
        {
            storeDB = storeDb;
            giftCards = giftCardService;
            this.emailSender = emailSender;
        }

        //
        // GET: /GiftCard/Buy

        public IActionResult Buy()
        {
            return View(new BuyGiftCardViewModel { SenderName = User.Identity?.Name });
        }

        //
        // POST: /GiftCard/Buy

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Buy(BuyGiftCardViewModel model)
        {
            var amount = decimal.Round(model.EffectiveAmount, 2);
            if (amount < BuyGiftCardViewModel.MinimumAmount || amount > BuyGiftCardViewModel.MaximumAmount)
            {
                ModelState.AddModelError(
                    nameof(model.CustomAmount),
                    $"Choose a gift-card amount between {BuyGiftCardViewModel.MinimumAmount:C0} and {BuyGiftCardViewModel.MaximumAmount:C0}.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var card = await giftCards.IssueAsync(
                amount,
                User.Identity!.Name,
                model.RecipientEmail!,
                model.RecipientName,
                string.IsNullOrWhiteSpace(model.SenderName) ? User.Identity!.Name : model.SenderName,
                model.Message);

            await emailSender.SendEmailAsync(
                card.RecipientEmail!,
                $"You've received a {card.InitialAmount:C} Music Store gift card",
                BuildGiftCardEmail(card));

            return RedirectToAction(nameof(Purchased), new { id = card.GiftCardId });
        }

        //
        // GET: /GiftCard/Purchased/5

        public async Task<IActionResult> Purchased(int id)
        {
            var card = await FindOwnedCardAsync(id);
            if (card == null)
            {
                return NotFound();
            }

            return View(card);
        }

        //
        // GET: /GiftCard/Index

        public async Task<IActionResult> Index()
        {
            var viewModel = new MyGiftCardsViewModel
            {
                PurchasedCards = await GetPurchasedCardsAsync()
            };

            return View(viewModel);
        }

        //
        // POST: /GiftCard/Balance

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Balance(string? code)
        {
            var viewModel = new MyGiftCardsViewModel
            {
                PurchasedCards = await GetPurchasedCardsAsync(),
                LookupCode = code
            };

            if (string.IsNullOrWhiteSpace(code))
            {
                viewModel.LookupError = "Enter a gift-card code to check its balance.";
            }
            else
            {
                var card = await giftCards.GetActiveByCodeAsync(code);
                if (card == null)
                {
                    viewModel.LookupError = "We couldn't find an active gift card with that code.";
                }
                else
                {
                    viewModel.LookupResult = card;
                }
            }

            return View(nameof(Index), viewModel);
        }

        private async Task<System.Collections.Generic.List<GiftCard>> GetPurchasedCardsAsync()
        {
            var username = User.Identity!.Name;
            var cards = await storeDB.GiftCards
                .Where(g => g.PurchaserUsername == username)
                .ToListAsync();

            return cards.OrderByDescending(g => g.CreatedDate).ToList();
        }

        private async Task<GiftCard?> FindOwnedCardAsync(int id)
        {
            var cards = await storeDB.GiftCards
                .Where(g => g.GiftCardId == id)
                .Take(1)
                .ToListAsync();

            var card = cards.FirstOrDefault();
            if (card == null || !string.Equals(card.PurchaserUsername, User.Identity!.Name, StringComparison.Ordinal))
            {
                return null;
            }

            return card;
        }

        private static string BuildGiftCardEmail(GiftCard card)
        {
            var sender = string.IsNullOrWhiteSpace(card.SenderName) ? "A friend" : card.SenderName;
            var greeting = string.IsNullOrWhiteSpace(card.RecipientName) ? "Hello" : $"Hi {card.RecipientName}";
            var message = string.IsNullOrWhiteSpace(card.Message)
                ? string.Empty
                : $"<p><em>\"{card.Message}\"</em></p>";

            return $@"<p>{greeting},</p>
<p>{sender} has sent you a <strong>{card.InitialAmount:C}</strong> MVC Music Store gift card!</p>
{message}
<p>Your gift-card code is:</p>
<p style=""font-size:1.4em;font-weight:bold;letter-spacing:2px;"">{card.Code}</p>
<p>Apply it at checkout to pay for any music in the store.</p>
<p>Enjoy the music!<br/>The MVC Music Store</p>";
        }
    }
}
