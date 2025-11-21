using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using user_service.Model;

namespace user_service.Data
{
    public class UserDButils : DbContext
    {
        public UserDButils(DbContextOptions<UserDButils> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

        }
    }
}
