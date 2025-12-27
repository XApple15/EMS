using Microsoft.EntityFrameworkCore;
using monitoring_service.Model;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace monitoring_service.Data
{
    public class MonitorDbUtils : DbContext
    {
        public MonitorDbUtils(DbContextOptions<MonitorDbUtils> options) : base(options)
        {
        }

        public DbSet<Consumption> Consumptions { get; set; }
        public DbSet<Device> Devices { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

        }
    }
}
