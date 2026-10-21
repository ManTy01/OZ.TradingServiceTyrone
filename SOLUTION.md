# Solution notes

## 1. Event model vs a mutable trade row

Each inbound message is an **append-only event** (`TradeEvents` / `CashEvents`). `externalRef` is **not** unique.

A single current-row table unique on `externalRef` cannot do all of:

- keep a re-send for audit without applying it twice
- apply a later correction
- report a snapshot for Monday that ignores Tuesday’s correction

`IsApplied` is the book. `Decision` (`Accepted`, `DuplicateIgnored`, `CorrectionApplied`) is what happened, for logs and ops. Duplicates are inserted with `IsApplied = false`. Corrections insert a **new** applied row; old applied rows stay.

Internal ids are integers for reference data (easy seed) and GUIDs for events.

## 2. Duplicate vs correction

Policy lives in `IngestionDecisionService` (no EF). Only **applied** `asOf` values are considered:

| Incoming `asOf` vs max applied `asOf` for that `externalRef` | Decision |
|---|---|
| no applied row | `Accepted` |
| `<=` latest applied | `DuplicateIgnored` |
| `>` latest applied | `CorrectionApplied` |

Time decides, not a field-by-field payload diff. Same numbers with a later `asOf` is still a new version. Different numbers with the same/earlier `asOf` is still a duplicate.

`asOf` is the upstream version clock (UTC `datetimeoffset`). `ReceivedAt` is when this service stored the row; it is not used for versioning.

Cash uses the same policy on `CashEvents`. Trade and cash refs are separate streams (`TRX-1001` vs `FEE-1001`).

## 3. Snapshot cutoff, prices, rounding

HTTP: `GET /api/accounts/{accountCode}/snapshots?date=yyyy-MM-dd`  
SQL: `EXEC dbo.usp_GetPortfolioSnapshot @AccountCode, @AsOfDate`

Both use:

- known account or 404 / `THROW 50001`
- `IsApplied = 1`
- `AsOf < start of next calendar day UTC` (half-open range; SARGable — no `CAST(AsOf AS date)`)
- `TradeDate <= @AsOfDate`
- latest version per `externalRef` (`AsOf` desc, `ReceivedAt` desc)

Market price is the latest `MarketPrices` row with `AsOfDate <= snapshot date`. One price per instrument per day (unique index). Missing price: API returns 400; the proc omits the holding (`INNER JOIN`).

Money is stored `decimal(18,6)`. API and proc round **display** amounts to 2 decimal places (`MidpointRounding.AwayFromZero` in C#; `ROUND(..., 2)` in T-SQL). Quantity is not treated as currency (up to 6 dp).

## 4. Average cost

**Product rule:** running average cost per instrument, processing **effective** trades in `TradeDate`, `AsOf`, `ReceivedAt` order.

- **Buy** — quantity up; cost pool += qty × price.
- **Sell** — quantity down; cost pool reduced by `min(sell, current qty) × current average`. Remaining unit cost does not change.
- Flat or short — cost pool 0.

Unrealized P/L (USD) = market value − (quantity × unit cost).

The C# snapshot (`BuildPositions`) implements this loop. The stored procedure uses a **recursive CTE** over ordered trades so a sell sees the average **immediately before** that sell. That matches C# when buys and sells interleave (e.g. buy 10 @ 10, sell 5, buy 10 @ 20 → unit cost 16.67, not 15).

Simplifications (stated): no lots, no wash-sale, no currency-specific cost buckets beyond converting the native pool to USD at the snapshot FX rate.

## 5. FX

Seed: `USD` / `ZAR` / `18.72` on 2026-10-01 and 2026-10-02. **1 USD = 18.72 ZAR.**

ZAR → USD is **divide** by the rate, never multiply. USD is left as-is. Latest rate with `AsOfDate <= snapshot date` (unique on base + quote + date).

AAPL is USD. NPN is ZAR at 3744 so `3744 / 18.72 = 200` USD exactly, which makes conversion easy to show.

Missing USD-ZAR: API returns 400 if a ZAR amount must be converted. The proc `OUTER APPLY`s the rate; a missing rate can null ZAR components (seed always has a rate for the demo dates).

## 6. Cash

`POST /api/cash` with `FEE` or `CASH_ADJUSTMENT`. Amount is **signed**. Fees must be negative (400 otherwise). Currency is USD or ZAR only.

Snapshot sums **effective** cash by currency, converts to USD, and adds `cashTotalUsd` into `totalValue` (positions + cash). `totalMarketValue` is positions only.

## 7. Feature flag (rollout)

`FeatureManagement:DuplicateAndCorrectionHandling` via `Microsoft.FeatureManagement`.

| Flag | Behaviour |
|---|---|
| on (default locally) | append-only decision table above |
| off | if `externalRef` already exists → **409**, no insert |

Off is the **legacy** path (reject duplicates, no corrections), not “insert everything and double-count.” Disable the new behaviour in an environment without corrupting the book. Drive the same key from env vars / Azure App Configuration per environment; Production can stay `false` until soak.

The snapshot always reads `IsApplied` and as-of versions. The flag only changes **ingest**.

## 8. Logging

Serilog console, request logging middleware, structured templates (not interpolated blobs):

- ingest: `{ExternalRef} {Decision} {AccountCode} {AsOf} {IsApplied} FeatureEnabled=`
- snapshot: `{AccountCode} {Date} {PositionCount} {TotalMarketValue} {TotalValue}`

Queryable in Seq / Application Insights later. HTTP errors use `ProblemDetails`.

## 9. SQL artifact, indexes, volume

`scripts/usp_GetPortfolioSnapshot.sql` — parameterized, set-based, two result sets (instrument lines, account totals). Window functions pick the current event version and the latest price. Recursive CTE is used only for running cost (a cursor would be worse; a buy-weighted shortcut would disagree with C# on interleaved trades).

`scripts/Indexes.sql` documents access paths **already created by EF**:

- `IX_TradeEvents_Account_AsOf_Applied` — `(AccountId, AsOf)` filtered `IsApplied = 1`, INCLUDE of valuation columns
- `IX_TradeEvents_ExternalRef_AsOf` — ingest lookup
- same pattern on `CashEvents`
- unique `(InstrumentId, AsOfDate)` on prices; unique `(BaseCurrency, QuoteCurrency, AsOfDate)` on FX; unique `AccountCode`

**SARGability:** `AsOf < @Cutoff` and `AccountId = @AccountId` can seek the filtered index. Do not wrap `AsOf` in `CAST`/`CONVERT` to `date`. `TradeDate <= @AsOfDate` is a residual predicate on that seek; putting a second range on the same index is not useful.

**At higher volume (mature codebase):**

- Keep the covering filtered index; review INCLUDE vs key lookup in actual plans.
- Partition `TradeEvents` / `CashEvents` on `TradeDate` (or month) when a single partition no longer fits memory/IO for a typical as-of query.
- Archive applied events older than the retention window for **live** snapshots into `TradeEventHistory` / `CashEventHistory`; the proc would `UNION ALL` only if a request dates into archive. Current rows stay hot.
- Recursive cost is O(trades per instrument). Fine here. At large books, persist a daily position/cost snapshot table as-of each business date (the API already returns that shape) and have the proc read the snapshot, with events used only to rebuild.
- Parameterize everything (already); avoid sniffing issues with local variables for `@Cutoff` (already declared).

API and proc are **two implementations** of the same rules (HTTP for apps; SQL for reporting / SSMS / older hosts). They are verified to match on the demo book. The API does not `EXEC` the proc; that keeps Task 1 and Task 2 independently demonstrable. A later change could make the API a thin wrapper around the proc (single engine).

## 10. .NET Framework 4.8 vs .NET 8

If this landed beside an existing 4.8 platform:

| | .NET 4.8 | This service (.NET 8) |
|---|---|---|
| Data access | EF6 / `System.Data.SqlClient` / ADO `SqlCommand` for the proc | EF Core 8 / `Microsoft.Data.SqlClient` (`TrustServerCertificate` for LocalDB) |
| Hosting | IIS + `Global.asax`, no generic host | `WebApplication`, built-in DI |
| Config | `web.config` | `appsettings` + env vars; feature flag package |
| Logging | log4net / NLog, often unstructured | `ILogger` + Serilog properties |
| Packages | packages.config, old csproj | SDK-style, central versioning possible |
| Tests | often not in-process | `WebApplicationFactory` against real LocalDB |

The **stored procedure** is the integration point that a 4.8 app can call with `SqlCommand` without taking a .NET 8 dependency. New behaviour (idempotent ingest) should stay on the new host and be flagged, not copied into 4.8.

## 11. UI (Blazor)

`TradeIngestion.Blazor` is a **Blazor Server** app (interactive server render mode). It is an HTTP client only: no ingest rules, no cost basis, no EF.

It calls:

- `GET /api/accounts` — dropdown
- `GET /api/instruments?date=` — AAPL / NPN plus market close (native and USD; ZAR ÷ FX rate)
- `GET /api/accounts/{code}/snapshots?date=` — positions, cash, totals
- `GET /api/accounts/{code}/events` — recent trades and cash, including duplicates (`IsApplied = false`)
- `POST /api/trades` — buy/sell (API rejects a sell larger than the held quantity)
- `POST /api/cash` — USD settlement of qty × USD market close, then $15 USD `FEE`; deposit/withdraw as `CASH_ADJUSTMENT`

CORS on the API allows the Blazor origin (`https://localhost:7123`, `http://localhost:5209`). Enums are JSON strings (`Accepted`, not `1`) so the UI and Swagger stay readable. Amounts are formatted with `CultureInfo.InvariantCulture` so decimals use a full stop regardless of machine locale.

Kendo was not used; Bootstrap is enough for the optional UI in the brief. Valuation stays in the API and the SQL proc.

## 12. Scope

The brief is covered: trade and cash ingest, duplicate/correction handling with a feature flag, USD snapshot (API and SQL proc), FX for one pair, seed data, automated test, demo script, structured logging, and a Blazor UI on the API.

## 13. How AI tools were used

I used **Cursor** as a senior pair-programmer, not as an autocompleted solution. I implemented in Visual Studio, ran LocalDB, wrote and rewrote the stored procedure myself, and only asked the model to help me with formatting the following files (SOLUTION, README).

### Prompts (representative)

**1. Understand the brief**

> I’m building an ASP.NET Core 8 API with EF and SQL Server. Help me think about solution structure: what projects/folders would you use and why? I want Core vs Infrastructure vs Api.

**2. Lock scope and LocalDB**

> Include the optionals. I want EF against actual LocalDB. API and SQL first, Blazor later.

*Thinking:* InMemory EF would fail the SQL proc and indexes. Cash and FX are in the brief as optionals; I wanted them in the design from day one so the snapshot was not a rewrite later.

**3. Visual Studio scaffolding, not CLI**
> I’m going to append trade events instead of updating one row. Duplicate = same/earlier as_of. Correction = later as_of. Does that match this brief? What should a snapshot for date D include?

**4. Tooling / “I’m stuck”**

> Add-Migration is not recognized in PowerShell. Package Manager Console vs dotnet ef?

> LocalDB connection string for EF Core 8?

**5. Review your SQL**
 
> Here’s my proc. Does ROW_NUMBER per ExternalRef handle corrections? Is AsOf < next day SARGable? I used a recursive CTE for average cost to match C# — overkill?

**6. I wrote the proc; I used the model as a reviewer**

> Is AccountCode unique? Can there be two MarketPrices for the same instrument and date? Two FxRates for the same pair and date? What should happen to cost when buys and sells interleave?

**7. Docs**

> Write README and SOLUTION.md from what is actually in the repo. Update the UI section now that Blazor exists.

### Video
https://www.loom.com/share/c569ce4562c443d4a194883e9ca4b37c