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
    /// <summary>
    /// Maps TradeEvent → TradeEvents. ExternalRef is NOT unique (need original + duplicate + correction).
    /// Enums stored as strings so SSMS shows Buy / CorrectionApplied.
    /// </summary>
    public class TradeEventConfiguration : IEntityTypeConfiguration<TradeEvent>
    {
        public void Configure(EntityTypeBuilder<TradeEvent> builder)
        {
            builder.ToTable("TradeEvents");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.ExternalRef)
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(e => e.Quantity)
                .HasPrecision(18, 6);
            builder.Property(e => e.Price)
                .HasPrecision(18, 6);
            builder.Property(e => e.Currency)
                .HasMaxLength(3)
                .IsRequired();
            builder.Property(e => e.Side)
                .HasConversion<string>()
                .HasMaxLength(16);
            builder.Property(e => e.Decision)
                .HasConversion<string>()
                .HasMaxLength(32);
            builder.HasOne(e => e.Account)
                .WithMany(a => a.TradeEvents)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(e => e.Instrument)
                .WithMany(i => i.TradeEvents)
                .HasForeignKey(e => e.InstrumentId)
                .OnDelete(DeleteBehavior.Restrict);
            // Ingest: find existing versions of this upstream id.
            builder.HasIndex(e => new { e.ExternalRef, e.AsOf })
                .HasDatabaseName("IX_TradeEvents_ExternalRef_AsOf");
            // Snapshot/SQL proc: this account, as-of cutoff, applied rows only. INCLUDE avoids extra lookups.
            builder.HasIndex(e => new { e.AccountId, e.AsOf })
                .IncludeProperties(e => new
                {
                    e.ExternalRef,
                    e.InstrumentId,
                    e.Side,
                    e.Quantity,
                    e.Price,
                    e.Currency,
                    e.TradeDate,
                    e.IsApplied,
                    e.Decision
                })
                .HasFilter("[IsApplied] = 1")
                .HasDatabaseName("IX_TradeEvents_Account_AsOf_Applied");
        }
    }
}
