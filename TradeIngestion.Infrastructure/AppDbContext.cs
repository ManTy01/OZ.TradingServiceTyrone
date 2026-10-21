using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeIngestion.Core.Entities;

namespace TradeIngestion.Infrastructure
{
    /// <summary>
    /// EF door to LocalDB. Each DbSet is a table. Configurations in Configuration/ define keys, indexes, seed.
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<Instrument> Instruments => Set<Instrument>();
        public DbSet<MarketPrice> MarketPrices => Set<MarketPrice>();
        public DbSet<FxRate> FxRates => Set<FxRate>();
        public DbSet<TradeEvent> TradeEvents => Set<TradeEvent>();
        public DbSet<CashEvent> CashEvents => Set<CashEvent>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Picks up every IEntityTypeConfiguration<> in this project (AccountConfiguration, etc.).
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}
