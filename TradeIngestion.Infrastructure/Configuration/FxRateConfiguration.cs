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
    /// <summary>Rate = ZAR per 1 USD. Unique per pair+day. Convert ZAR to USD by dividing.</summary>
    public class FxRateConfiguration : IEntityTypeConfiguration<FxRate>
    {
        public void Configure(EntityTypeBuilder<FxRate> builder)
        {
            builder.ToTable("FxRates");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.BaseCurrency)
                .HasMaxLength(3)
                .IsRequired();
            builder.Property(e => e.QuoteCurrency)
                .HasMaxLength(3)
                .IsRequired();
            builder.Property(e => e.Rate)
                .HasPrecision(18, 6);
            builder.HasIndex(e => new { e.BaseCurrency, e.QuoteCurrency, e.AsOfDate })
                .IsUnique();
            builder.HasData(
                new FxRate
                {
                    Id = 1,
                    BaseCurrency = "USD",
                    QuoteCurrency = "ZAR",
                    AsOfDate = new DateOnly(2026, 10, 1),
                    Rate = 18.72m
                },
                new FxRate
                {
                    Id = 2,
                    BaseCurrency = "USD",
                    QuoteCurrency = "ZAR",
                    AsOfDate = new DateOnly(2026, 10, 2),
                    Rate = 18.72m
                });
        }
    }
}
