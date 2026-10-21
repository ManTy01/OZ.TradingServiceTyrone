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
    /// <summary>Maps Account → table Accounts, unique AccountCode, seed ACC-001 / ACC-002.</summary>
    public class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public void Configure(EntityTypeBuilder<Account> builder)
        {
            builder.ToTable("Accounts");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.AccountCode)
                .HasMaxLength(32)
                .IsRequired();
            builder.Property(e => e.Name)
                .HasMaxLength(128)
                .IsRequired();
            // One ACC-001. The API looks up by this code, not by Id.
            builder.HasIndex(e => e.AccountCode)
                .IsUnique();
            builder.HasData(
                new Account { Id = 1, AccountCode = "ACC-001", Name = "Demo Equity Account" },
                new Account { Id = 2, AccountCode = "ACC-002", Name = "Demo Second Account" });
        }
    }
}
