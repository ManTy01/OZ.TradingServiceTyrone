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
    /// <summary>Maps Instrument. Unique ISIN. Seed AAPL (USD) and NPN (ZAR) for the FX demo.</summary>
    public class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
    {
        public void Configure(EntityTypeBuilder<Instrument> builder)
        {
            builder.ToTable("Instruments");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Isin)
                .HasMaxLength(12)
                .IsRequired();
            builder.Property(e => e.Symbol)
                .HasMaxLength(16)
                .IsRequired();
            builder.Property(e => e.Currency)
                .HasMaxLength(3)
                .IsRequired();
            builder.HasIndex(e => e.Isin)
                .IsUnique();
            builder.HasData(
                new Instrument
                {
                    Id = 1,
                    Isin = "US0378331005",
                    Symbol = "AAPL",
                    Currency = "USD"
                },
                new Instrument
                {
                    Id = 2,
                    Isin = "ZAE000015889",
                    Symbol = "NPN",
                    Currency = "ZAR"
                });
        }
    }
}
