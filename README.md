# Trade Ingestion and Portfolio Snapshot Service

ASP.NET Core 8 Web API that ingests trade and cash events from an upstream administration system, stores them in SQL Server (LocalDB), and exposes a daily USD portfolio snapshot. Duplicate re-sends are not applied; later corrections are. A T-SQL stored procedure computes the same snapshot in the database.

## Requirements

- .NET 8 SDK
- SQL Server LocalDB (`(localdb)\MSSQLLocalDB`) — included with Visual Studio
- Visual Studio 2022+ or the `dotnet` CLI

Check LocalDB:

```powershell
sqllocaldb start MSSQLLocalDB
sqllocaldb info MSSQLLocalDB
```

## Solution layout

| Project | Role |
|---|---|
| `TradeIngestion.Api` | HTTP, Swagger, feature flags, Serilog |
| `TradeIngestion.Core` | Entities, ingest decision policy, snapshot contracts |
| `TradeIngestion.Infrastructure` | EF Core, SQL Server, ingest/snapshot services |
| `TradeIngestion.Tests` | Decision unit tests + HTTP end-to-end test |
| `TradeIngestion.Blazor` | Snapshot + Trade UI (HTTP client only) |
| `scripts/` | Stored procedure, index DDL, demo `.http` file |

Open `OZ.TradingServiceTyrone.slnx`.

## Configuration

Development connection string (`TradeIngestion.Api/appsettings.Development.json`):

```
Server=(localdb)\MSSQLLocalDB;Database=TradeIngestion;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

Feature flag (`appsettings.json` / `appsettings.Development.json`):

```json
"FeatureManagement": {
  "DuplicateAndCorrectionHandling": true
}
```

- `true` — re-sends are stored and ignored; later `asOf` is a correction.
- `false` — a second message with the same `externalRef` returns `409 Conflict` (legacy).

## Run locally

1. Start LocalDB (command above).
2. Set **TradeIngestion.Api** as the startup project.
3. F5 (https profile) or:

```powershell
dotnet run --project TradeIngestion.Api
```

On Development startup the API runs `Database.Migrate()`, which creates `TradeIngestion` and seed data (accounts `ACC-001` / `ACC-002`, AAPL, NPN, prices, USD-ZAR).

Swagger: `https://localhost:7129/swagger` (or `http://localhost:5117/swagger` for the http profile).

Blazor (second startup project, or `dotnet run --project TradeIngestion.Blazor`): `https://localhost:7123`. **Snapshot** is `/`; **Trade** is `/trade` (buy/sell listed instruments, $15 USD fee after an applied trade, deposit/withdraw). The API rejects a sell larger than the held quantity.

To apply migrations from the Package Manager Console instead (Default project = `TradeIngestion.Infrastructure`):

```powershell
Update-Database -StartupProject TradeIngestion.Api
```

CLI equivalent:

```powershell
dotnet ef database update --project TradeIngestion.Infrastructure --startup-project TradeIngestion.Api
```

## Demo

With the API running, open `TradeIngestion.Api/TradeIngestion.Api.http` (or `scripts/demo.http`) and run the requests in order.

The `.http` file has two sequences: **ACC-001** on 1–2 October 2026, then **ACC-002** on 15–16 October 2026. The table below is the ACC-002 run (same numbers as ACC-001). Blazor defaults the as-of date to today.

| Step | Expected |
|---|---|
| POST original BUY 120 | `201` Accepted |
| POST same body | `200` DuplicateIgnored |
| GET snapshot `2026-10-15` | qty **120**, marketValue **22320**, totalValue **22320** |
| POST correction qty 100, `asOf` 16 Oct | `200` CorrectionApplied |
| GET snapshot `2026-10-15` | still qty **120** |
| GET snapshot `2026-10-16` | qty **100**, marketValue **18600** |
| POST FEE `-15` USD | `201` |
| GET `2026-10-15` | totalValue **22305** |
| GET `2026-10-16` | totalValue **18585** |

Re-running the file requires new `externalRef` values or deleting `DEMO-*` rows from `TradeEvents` / `CashEvents`.

## Tests

```powershell
dotnet test
```

Or Test Explorer: `IngestDuplicateCorrectionAndCash_SnapshotsRespectAsOf` and `IngestionDecisionServiceTests`.

The e2e test uses the same LocalDB catalog, account `ACC-002`, and `E2E-*` refs (cleaned at the start of the test).

## SQL snapshot (Task 2)

Script: `scripts/usp_GetPortfolioSnapshot.sql`  
Index DDL (already applied by EF; kept as documentation): `scripts/Indexes.sql`

In SQL Server Object Explorer, connect to `(localdb)\MSSQLLocalDB`, database `TradeIngestion`, then:

```sql
EXEC dbo.usp_GetPortfolioSnapshot @AccountCode = N'ACC-001', @AsOfDate = '2026-10-01';
EXEC dbo.usp_GetPortfolioSnapshot @AccountCode = N'ACC-001', @AsOfDate = '2026-10-02';
```

Two result sets: instrument lines, then account totals (cash included). Numbers must match `GET /api/accounts/{account}/snapshots?date=`.

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -d TradeIngestion -i scripts\usp_GetPortfolioSnapshot.sql
```

## Design notes

See [SOLUTION.md](SOLUTION.md) for the event model, duplicate vs correction, cost basis, FX, feature flag, logging, and SQL performance notes.

## Video
https://www.loom.com/share/c569ce4562c443d4a194883e9ca4b37c