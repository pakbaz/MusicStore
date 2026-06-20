using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MvcMusicStore.Models;

namespace MvcMusicStore.Services;

/// <summary>
/// Periodically scans shopping carts and emails signed-in users whose carts have gone stale.
/// Anonymous carts (GUID CartId) are ignored because they have no associated account/consent.
/// </summary>
public sealed class AbandonedCartReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IOptions<AbandonedCartOptions> options;
    private readonly ILogger<AbandonedCartReminderWorker> logger;

    public AbandonedCartReminderWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AbandonedCartOptions> options,
        ILogger<AbandonedCartReminderWorker> logger)
    {
        this.scopeFactory = scopeFactory;
        this.options = options;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Abandoned-cart reminder worker is disabled.");
            return;
        }

        await DelaySafelyAsync(TimeSpan.FromSeconds(Math.Max(0, options.Value.StartupDelaySeconds)), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var currentOptions = options.Value;
            try
            {
                await RunScanAsync(currentOptions, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Abandoned-cart reminder scan failed.");
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, currentOptions.ScanIntervalMinutes));
            await DelaySafelyAsync(interval, stoppingToken);
        }
    }

    private async Task RunScanAsync(AbandonedCartOptions currentOptions, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MusicStoreEntities>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var emailService = scope.ServiceProvider.GetRequiredService<StoreEmailService>();

        // DateCreated is written with DateTime.Now, so compare inactivity in local time.
        var inactivityCutoff = DateTime.Now.AddMinutes(-Math.Max(1, currentOptions.ReminderAfterMinutes));
        var utcNow = DateTime.UtcNow;
        var batchSize = Math.Max(1, currentOptions.BatchSize);

        var allItems = await db.Carts.ToListAsync(cancellationToken);
        var cartsByOwner = allItems
            .Where(c => !string.IsNullOrWhiteSpace(c.CartId) && c.Count > 0)
            .GroupBy(c => c.CartId!);

        var sent = 0;
        foreach (var group in cartsByOwner)
        {
            if (cancellationToken.IsCancellationRequested || sent >= batchSize)
            {
                break;
            }

            var items = group.ToList();

            // Only nudge carts that have been untouched long enough.
            if (items.Max(i => i.DateCreated) > inactivityCutoff)
            {
                continue;
            }

            // CartId equals the username for signed-in users; anonymous GUID carts won't resolve.
            var user = await userManager.FindByNameAsync(group.Key);
            if (user is null || string.IsNullOrWhiteSpace(user.Email) || !user.AbandonedCartOptIn)
            {
                continue;
            }

            var signature = BuildSignature(items);
            if (!ShouldRemind(user, signature, utcNow, currentOptions))
            {
                continue;
            }

            var dispatched = await emailService.SendAbandonedCartReminderAsync(user, items, cancellationToken);
            if (!dispatched)
            {
                continue;
            }

            user.LastAbandonedCartReminderUtc = utcNow;
            user.LastRemindedCartSignature = signature;
            await userManager.UpdateAsync(user);
            sent++;
        }

        if (sent > 0)
        {
            logger.LogInformation("Dispatched {Count} abandoned-cart reminder(s).", sent);
        }
    }

    private static bool ShouldRemind(ApplicationUser user, string signature, DateTime utcNow, AbandonedCartOptions currentOptions)
    {
        if (user.LastAbandonedCartReminderUtc is null)
        {
            return true;
        }

        // The cart changed since the last reminder — worth another nudge.
        if (!string.Equals(user.LastRemindedCartSignature, signature, StringComparison.Ordinal))
        {
            return true;
        }

        // Same cart, but enough time has elapsed to remind again.
        var resendWindow = TimeSpan.FromHours(Math.Max(1, currentOptions.ResendAfterHours));
        return utcNow - user.LastAbandonedCartReminderUtc.Value >= resendWindow;
    }

    private static string BuildSignature(IEnumerable<Cart> items) =>
        string.Join("|", items.OrderBy(i => i.AlbumId).Select(i => $"{i.AlbumId}:{i.Count}"));

    private static async Task DelaySafelyAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            // Shutdown requested.
        }
    }
}
