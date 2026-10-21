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
    /// <summary>Same versioning as trades: ExternalRef is not unique. Filtered index for applied snapshot rows.</summary>
    public class CashEventConfiguration : IEntityTypeConfiguration<CashEvent>
    {
        public void Configure(EntityTypeBuilder<CashEvent> builder)
        {
            builder.ToTable("CashEvents");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.ExternalRef)
                .HasMaxLength(64)
                .IsRequired();
            builder.Property(e => e.Amount)
                .HasPrecision(18, 6);
            builder.Property(e => e.Currency)
                .HasMaxLength(3)
                .IsRequired();
            builder.Property(e => e.Description)
                .HasMaxLength(256);
            builder.Property(e => e.Type)
                .HasConversion<string>()
                .HasMaxLength(32);
            builder.Property(e => e.Decision)
                .HasConversion<string>()
                .HasMaxLength(32);
            builder.HasOne(e => e.Account)
                .WithMany(a => a.CashEvents)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(e => new { e.ExternalRef, e.AsOf })
                .HasDatabaseName("IX_CashEvents_ExternalRef_AsOf");
            builder.HasIndex(e => new { e.AccountId, e.AsOf })
                .IncludeProperties(e => new
                {
                    e.ExternalRef,
                    e.Type,
                    e.Amount,
                    e.Currency,
                    e.TradeDate,
                    e.IsApplied,
                    e.Decision
                })
                .HasFilter("[IsApplied] = 1")
                .HasDatabaseName("IX_CashEvents_Account_AsOf_Applied");
        }
    }
}
