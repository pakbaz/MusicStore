using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MvcMusicStore.Models
{
    /// <summary>
    /// Identity user store tuned for the Azure Cosmos DB provider. The default
    /// <see cref="UserStore{TUser}"/> resolves a user's roles with a server-side join between the
    /// user-role and role containers, which the Cosmos provider cannot translate ("A query can
    /// only reference a single root entity type"). That join runs on every sign-in via the claims
    /// principal factory, so without this override sign-in (login, register) fails. Here the lookup
    /// is split into two single-container queries and combined client-side.
    /// </summary>
    public class CosmosUserStore : UserStore<ApplicationUser>
    {
        public CosmosUserStore(ApplicationDbContext context, IdentityErrorDescriber? describer = null)
            : base(context, describer)
        {
        }

        public override async Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(user);

            var userId = user.Id;
            var roleIds = await Context.Set<IdentityUserRole<string>>()
                .Where(userRole => userRole.UserId == userId)
                .Select(userRole => userRole.RoleId)
                .ToListAsync(cancellationToken);

            var roleNames = new List<string>();
            foreach (var roleId in roleIds)
            {
                var roleName = await Context.Set<IdentityRole>()
                    .Where(role => role.Id == roleId)
                    .Select(role => role.Name)
                    .FirstOrDefaultAsync(cancellationToken);

                if (!string.IsNullOrEmpty(roleName))
                {
                    roleNames.Add(roleName);
                }
            }

            return roleNames;
        }
    }
}
