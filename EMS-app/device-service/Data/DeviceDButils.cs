using device_service.Model;
using Microsoft.EntityFrameworkCore;

namespace device_service.Data
{
    public class DeviceDButils : DbContext
    {
        public DeviceDButils(DbContextOptions<DeviceDButils> options) : base(options)
        {
        }

        public DbSet<Device> Devices { get; set; }
        public DbSet<User> Users { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Add unique index on AuthId for idempotency
            modelBuilder.Entity<User>()
                .HasIndex(u => u.AuthId)
                .IsUnique()
                .HasDatabaseName("IX_Users_AuthId");
        }
    }
}
