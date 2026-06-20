using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MvcMusicStore.Models
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class.
    public class ApplicationUser : Microsoft.AspNetCore.Identity.IdentityUser
    {
        /// <summary>Consent for newsletter / new-release marketing email. Opt-in (defaults to false).</summary>
        public bool EmailMarketingOptIn { get; set; }

        /// <summary>Whether abandoned-cart reminder emails are allowed. Opt-out (defaults to true).</summary>
        public bool AbandonedCartOptIn { get; set; } = true;

        /// <summary>Stable token embedded in one-click unsubscribe links.</summary>
        public string? UnsubscribeToken { get; set; }

        /// <summary>Timestamp of the last abandoned-cart reminder sent to this user (UTC).</summary>
        public DateTime? LastAbandonedCartReminderUtc { get; set; }

        /// <summary>Signature of the cart contents at the last reminder, used to avoid duplicate nudges.</summary>
        public string? LastRemindedCartSignature { get; set; }
    }

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>().ToContainer("Identity_Users");
            builder.Entity<IdentityRole>().ToContainer("Identity_Roles");
            builder.Entity<IdentityUserClaim<string>>().ToContainer("Identity_UserClaims");
            builder.Entity<IdentityUserRole<string>>().ToContainer("Identity_UserRoles");
            builder.Entity<IdentityUserLogin<string>>().ToContainer("Identity_UserLogins");
            builder.Entity<IdentityRoleClaim<string>>().ToContainer("Identity_RoleClaims");
            builder.Entity<IdentityUserToken<string>>().ToContainer("Identity_UserTokens");

            // Cosmos only supports the '_etag' property as a concurrency token, but ASP.NET Identity
            // configures 'ConcurrencyStamp' as one. Disable it so the model validates on Cosmos.
            builder.Entity<ApplicationUser>().Property(u => u.ConcurrencyStamp).IsConcurrencyToken(false);
            builder.Entity<IdentityRole>().Property(r => r.ConcurrencyStamp).IsConcurrencyToken(false);

            // The Azure Cosmos DB provider does not support index definitions. ASP.NET Identity adds
            // indexes (e.g. NormalizedName / NormalizedUserName), so strip every index from the model.
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                foreach (var index in entityType.GetIndexes().ToList())
                {
                    entityType.RemoveIndex(index);
                }
            }
        }
    }
}
