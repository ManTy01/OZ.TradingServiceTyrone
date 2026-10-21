using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeIngestion.Core.Entities;

namespace TradeIngestion.Infrastructure.Configuration
{
    /// <summary>One close price per instrument per day (unique). Snapshot uses latest date &lt;= as-of.</summary>
    public class MarketPriceConfiguration : IEntityTypeConfiguration<MarketPrice>
    {
        public void Configure(EntityTypeBuilder<MarketPrice> builder)
        {
            builder.ToTable("MarketPrices");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Price)
                .HasPrecision(18, 6);
            builder.Property(e => e.Currency)
                .HasMaxLength(3)
                .IsRequired();
            builder.HasOne(e => e.Instrument)
                .WithMany(i => i.Prices)
                .HasForeignKey(e => e.InstrumentId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(e => new { e.InstrumentId, e.AsOfDate })
                .IsUnique();
            builder.HasData(
                new MarketPrice
                {
                    Id = 1,
                    InstrumentId = 1,
                    AsOfDate = new DateOnly(2026, 10, 1),
                    Price = 186.00m,
                    Currency = "USD"
                },
                new MarketPrice
                {
                    Id = 2,
                    InstrumentId = 1,
                    AsOfDate = new DateOnly(2026, 10, 2),
                    Price = 186.00m,
                    Currency = "USD"
                },
                new MarketPrice
                {
                    Id = 3,
                    InstrumentId = 2,
                    AsOfDate = new DateOnly(2026, 10, 1),
                    Price = 3744.00m,
                    Currency = "ZAR"
                },
                new MarketPrice
                {
                    Id = 4,
                    InstrumentId = 2,
                    AsOfDate = new DateOnly(2026, 10, 2),
                    Price = 3744.00m,
                    Currency = "ZAR"
                });
        }
    }
}
