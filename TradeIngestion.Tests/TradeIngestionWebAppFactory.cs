using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeIngestion.Infrastructure;

namespace TradeIngestion.Tests
{
    public class TradeIngestionWebAppFactory : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            return base.CreateHost(builder);
        }
        public async Task ResetLedgerAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            db.TradeEvents.RemoveRange(
                db.TradeEvents.Where(e => e.ExternalRef.StartsWith("E2E-")));
            db.CashEvents.RemoveRange(
                db.CashEvents.Where(e => e.ExternalRef.StartsWith("E2E-")));
            await db.SaveChangesAsync();
        }
    }
}
