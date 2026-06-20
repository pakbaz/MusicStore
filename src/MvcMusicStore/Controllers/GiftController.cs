using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.Services;
using MvcMusicStore.ViewModels;

namespace MvcMusicStore.Controllers
{
    public class GiftController : Controller
    {
        private readonly MusicStoreEntities storeDB;
        private readonly IEmailSender emailSender;

        public GiftController(MusicStoreEntities storeDb, IEmailSender emailSender)
        {
            storeDB = storeDb;
            this.emailSender = emailSender;
        }

        //
        // GET: /Gift/Send/5

        [Authorize]
        public async Task<IActionResult> Send(int id)
        {
            var album = await storeDB.Albums.FirstOrDefaultAsync(a => a.AlbumId == id);
            if (album == null)
            {
                return NotFound();
            }

            var viewModel = new SendGiftViewModel
            {
                AlbumId = album.AlbumId,
                AlbumTitle = album.Title,
                ArtistName = album.ArtistName,
                AlbumArtUrl = album.GetDisplayThumbnailUrl(),
                AlbumPrice = album.Price,
                SenderName = User.Identity?.Name
            };

            return View(viewModel);
        }

        //
        // POST: /Gift/Send

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(SendGiftViewModel model)
        {
            var album = await storeDB.Albums.FirstOrDefaultAsync(a => a.AlbumId == model.AlbumId);
            if (album == null)
            {
                return NotFound();
            }

            // Always render from the authoritative catalog record.
            model.AlbumTitle = album.Title;
            model.ArtistName = album.ArtistName;
            model.AlbumArtUrl = album.GetDisplayThumbnailUrl();
            model.AlbumPrice = album.Price;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var gift = new AlbumGift
            {
                AlbumGiftId = await storeDB.NextAlbumGiftIdAsync(),
                Token = GenerateToken(),
                AlbumId = album.AlbumId,
                AlbumTitle = album.Title,
                AlbumPrice = album.Price,
                AlbumArtUrl = album.GetDisplayThumbnailUrl(),
                SenderUsername = User.Identity!.Name,
                SenderName = string.IsNullOrWhiteSpace(model.SenderName) ? User.Identity!.Name : model.SenderName,
                RecipientEmail = model.RecipientEmail,
                RecipientName = model.RecipientName,
                Message = model.Message,
                CreatedDate = DateTime.Now,
                IsRedeemed = false
            };

            storeDB.Gifts.Add(gift);
            await storeDB.SaveChangesAsync();

            var redeemUrl = BuildRedeemUrl(gift.Token);
            await emailSender.SendEmailAsync(
                gift.RecipientEmail!,
                $"{gift.SenderName} sent you \"{gift.AlbumTitle}\" on MVC Music Store",
                BuildGiftEmail(gift, redeemUrl));

            return RedirectToAction(nameof(Sent), new { id = gift.AlbumGiftId });
        }

        //
        // GET: /Gift/Sent/5

        [Authorize]
        public async Task<IActionResult> Sent(int id)
        {
            var gifts = await storeDB.Gifts
                .Where(g => g.AlbumGiftId == id)
                .Take(1)
                .ToListAsync();

            var gift = gifts.FirstOrDefault();
            if (gift == null || !string.Equals(gift.SenderUsername, User.Identity!.Name, StringComparison.Ordinal))
            {
                return NotFound();
            }

            ViewBag.RedeemUrl = BuildRedeemUrl(gift.Token);
            return View(gift);
        }

        //
        // GET: /Gift/Redeem?token=...

        [AllowAnonymous]
        public async Task<IActionResult> Redeem(string? token)
        {
            var gift = await FindGiftByTokenAsync(token);

            var viewModel = new RedeemGiftViewModel
            {
                Gift = gift,
                Token = token ?? string.Empty,
                IsSignedIn = User.Identity?.IsAuthenticated == true
            };

            return View(viewModel);
        }

        //
        // POST: /Gift/Redeem

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Redeem(string token, string confirm)
        {
            var gift = await FindGiftByTokenAsync(token);
            if (gift == null)
            {
                return View(new RedeemGiftViewModel { Token = token ?? string.Empty, IsSignedIn = true });
            }

            if (gift.IsRedeemed)
            {
                TempData["GiftMessage"] = "This gift has already been redeemed.";
                return RedirectToAction(nameof(Redeem), new { token });
            }

            var order = new Order
            {
                OrderId = await storeDB.NextOrderIdAsync(),
                Username = User.Identity!.Name,
                OrderDate = DateTime.Now,
                FirstName = gift.RecipientName ?? User.Identity!.Name,
                LastName = "(gift)",
                Email = gift.RecipientEmail,
                IsGift = true,
                Total = 0m,
                AmountDue = 0m,
                OrderDetails = new List<OrderDetail>
                {
                    new OrderDetail
                    {
                        OrderDetailId = 1,
                        AlbumId = gift.AlbumId,
                        OrderId = 0,
                        UnitPrice = 0m,
                        Quantity = 1
                    }
                }
            };
            order.OrderDetails[0].OrderId = order.OrderId;

            storeDB.Orders.Add(order);

            gift.IsRedeemed = true;
            gift.RedeemedDate = DateTime.Now;
            gift.RedeemedByUsername = User.Identity!.Name;
            gift.RedeemedOrderId = order.OrderId;

            await storeDB.SaveChangesAsync();

            TempData["GiftAlbumTitle"] = gift.AlbumTitle;
            TempData["GiftSenderName"] = gift.SenderName;
            return RedirectToAction(nameof(Redeemed), new { id = order.OrderId });
        }

        //
        // GET: /Gift/Redeemed/5

        [Authorize]
        public IActionResult Redeemed(int id)
        {
            var viewModel = new GiftRedeemedViewModel
            {
                OrderId = id,
                AlbumTitle = TempData["GiftAlbumTitle"] as string,
                SenderName = TempData["GiftSenderName"] as string
            };

            return View(viewModel);
        }

        private async Task<AlbumGift?> FindGiftByTokenAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var gifts = await storeDB.Gifts
                .Where(g => g.Token == token)
                .Take(1)
                .ToListAsync();

            return gifts.FirstOrDefault();
        }

        private string BuildRedeemUrl(string token)
        {
            return Url.Action(nameof(Redeem), "Gift", new { token }, Request.Scheme)
                ?? $"/Gift/Redeem?token={token}";
        }

        private static string GenerateToken()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        }

        private static string BuildGiftEmail(AlbumGift gift, string redeemUrl)
        {
            var sender = string.IsNullOrWhiteSpace(gift.SenderName) ? "A friend" : gift.SenderName;
            var greeting = string.IsNullOrWhiteSpace(gift.RecipientName) ? "Hello" : $"Hi {gift.RecipientName}";
            var message = string.IsNullOrWhiteSpace(gift.Message)
                ? string.Empty
                : $"<p><em>\"{gift.Message}\"</em></p>";

            return $@"<p>{greeting},</p>
<p>{sender} has sent you <strong>{gift.AlbumTitle}</strong> as a gift on the MVC Music Store!</p>
{message}
<p>Click the link below to redeem your gift and add it to your library:</p>
<p><a href=""{redeemUrl}"">{redeemUrl}</a></p>
<p>Enjoy the music!<br/>The MVC Music Store</p>";
        }
    }
}
