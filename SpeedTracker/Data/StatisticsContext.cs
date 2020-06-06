using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SpeedTracker.Data
{
    public class StatisticsContext : DbContext
    {
        public DbSet<Statistic> Statistics { get; set; }
        public DbSet<Event> Events{ get; set; }
        public DbSet<Server> Server { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite("Data Source=/data/stats.db");

    }
}
