using auth_service.Model.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using static System.Net.Mime.MediaTypeNames;

namespace auth_service.Data
{
    public class AuthDButils : IdentityDbContext<ApplicationUser>
    {
        public AuthDButils(DbContextOptions<AuthDButils> options) : base(options)
        {
        }

        public DbSet<ApplicationUser> ApplicationUsers { get; set; }
        


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var normalUserID = "5bf2857f-3d64-447f-824a-b8125a00a014";
            var adminUserID = "212d6dca-8a13-45cc-9492-919ec0c39e8f";

            var roles = new List<IdentityRole>
            {
                new IdentityRole {
                    Id = normalUserID,
                    ConcurrencyStamp = normalUserID,
                    Name = "Client",
                    NormalizedName = "CLIENT"
                },
                new IdentityRole {
                    Id = adminUserID,
                    ConcurrencyStamp = adminUserID,
                    Name = "Admin",
                    NormalizedName = "ADMIN"
                }
            };
            modelBuilder.Entity<IdentityRole>().HasData(roles);
        }
    }
}
