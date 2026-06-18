using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MvcMusicStore.Models
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class.
    public class ApplicationUser : Microsoft.AspNetCore.Identity.IdentityUser
    {
    }

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
    }
}