USE [TradeIngestion];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

/*
  Task 2 artifact — same snapshot as GET /api/accounts/{code}/snapshots?date=

  This is the version that runs in LocalDB (better than the first draft):
    - Resolve AccountId once (AccountCode is unique).
    - Temp tables so we can return TWO result sets (a WITH CTE only lasts one SELECT).
    - Recursive running cost so a SELL uses the average from just before that sell
      (same idea as C# SnapshotService.BuildPositions).
    - ZAR ? USD: DIVIDE by the rate (1 USD = Rate ZAR).
*/
CREATE OR ALTER PROCEDURE [dbo].[usp_GetPortfolioSnapshot]
    @AccountCode nvarchar(32),
    @AsOfDate    date
AS
BEGIN
    SET NOCOUNT ON;

    DROP TABLE IF EXISTS #PositionValued;
    DROP TABLE IF EXISTS #CashValued;

    -------------------------------------------------------------------------
    -- 1. Resolve the account once.
    --    AccountCode is unique, so all later queries can use AccountId.
    -------------------------------------------------------------------------
    DECLARE @AccountId int;

    SELECT @AccountId = Id
    FROM dbo.Accounts
    WHERE AccountCode = @AccountCode;

    IF @AccountId IS NULL
    BEGIN
        THROW 50001, 'Unknown account.', 1;
    END;

    -------------------------------------------------------------------------
    -- 2. As-of cutoff (same as SnapshotService in C#).
    --
    --    Snapshot date 1 Mar includes asOf before 2 Mar 00:00 UTC.
    --    "< cutoff" is a half-open range and stays SARGable (no CAST(AsOf AS date)).
    -------------------------------------------------------------------------
    DECLARE @Cutoff datetimeoffset(7);

    SET @Cutoff =
        TODATETIMEOFFSET(
            DATEADD(
                DAY,
                1,
                CONVERT(datetime2(7), @AsOfDate)
            ),
            '+00:00'
        );

    -------------------------------------------------------------------------
    -- 3. Latest APPLIED version of each trade (ExternalRef is not unique).
    --    VersionNumber = 1 wins: latest AsOf, then ReceivedAt if tied.
    --    Duplicates have IsApplied = 0 so they never enter this CTE.
    -------------------------------------------------------------------------
    ;WITH RankedTrades AS
    (
        SELECT
            te.ExternalRef,
            te.InstrumentId,
            i.Isin,
            i.Symbol,
            i.Currency AS InstrumentCurrency,
            te.Side,
            te.Quantity,
            te.Price,
            te.TradeDate,
            te.AsOf,
            te.ReceivedAt,
            ROW_NUMBER() OVER
            (
                PARTITION BY te.ExternalRef
                ORDER BY
                    te.AsOf DESC,
                    te.ReceivedAt DESC
            ) AS VersionNumber
        FROM dbo.TradeEvents AS te
        INNER JOIN dbo.Instruments AS i
            ON i.Id = te.InstrumentId
        WHERE te.AccountId = @AccountId
          AND te.IsApplied = 1
          AND te.AsOf < @Cutoff
          AND te.TradeDate <= @AsOfDate
    ),
    CurrentTrades AS
    (
        SELECT
            ExternalRef,
            InstrumentId,
            Isin,
            Symbol,
            InstrumentCurrency,
            Side,
            Quantity,
            Price,
            TradeDate,
            AsOf,
            ReceivedAt
        FROM RankedTrades
        WHERE VersionNumber = 1
    ),
    -- Time order per instrument so running cost sees sells in the right sequence.
    OrderedTrades AS
    (
        SELECT
            ExternalRef,
            InstrumentId,
            Isin,
            Symbol,
            InstrumentCurrency,
            Side,
            Quantity,
            Price,
            ROW_NUMBER() OVER
            (
                PARTITION BY InstrumentId
                ORDER BY
                    TradeDate,
                    AsOf,
                    ReceivedAt,
                    ExternalRef
            ) AS TradeNumber
        FROM CurrentTrades
    ),

    -------------------------------------------------------------------------
    -- 4. Running quantity + cost pool (matches C# BuildPositions).
    --
    --    BUY:  qty up,  pool += qty * price
    --    SELL: qty down, pool reduced at the CURRENT average
    --
    --    Recursive because a sell depends on the average from the previous row.
    -------------------------------------------------------------------------
    RunningPositions AS
    (
        SELECT
            ot.InstrumentId,
            ot.Isin,
            ot.Symbol,
            ot.InstrumentCurrency,
            ot.TradeNumber,
            CAST(
                CASE
                    WHEN ot.Side = N'Buy' THEN ot.Quantity
                    ELSE -ot.Quantity
                END
                AS decimal(38, 6)
            ) AS Quantity,
            CAST(
                CASE
                    WHEN ot.Side = N'Buy'
                        THEN ot.Quantity * ot.Price
                    ELSE 0
                END
                AS decimal(38, 6)
            ) AS CostPool
        FROM OrderedTrades AS ot
        WHERE ot.TradeNumber = 1

        UNION ALL

        SELECT
            ot.InstrumentId,
            ot.Isin,
            ot.Symbol,
            ot.InstrumentCurrency,
            ot.TradeNumber,
            CAST(
                CASE
                    WHEN ot.Side = N'Buy'
                        THEN rp.Quantity + ot.Quantity
                    ELSE rp.Quantity - ot.Quantity
                END
                AS decimal(38, 6)
            ) AS Quantity,
            CAST(
                CASE
                    WHEN ot.Side = N'Buy'
                        THEN rp.CostPool + (ot.Quantity * ot.Price)
                    WHEN rp.Quantity <= 0
                        THEN 0
                    WHEN rp.Quantity - ot.Quantity <= 0
                        THEN 0
                    ELSE
                        rp.CostPool
                        -
                        (
                            CASE
                                WHEN ot.Quantity < rp.Quantity
                                    THEN ot.Quantity
                                ELSE rp.Quantity
                            END
                        )
                        * (rp.CostPool / NULLIF(rp.Quantity, 0))
                END
                AS decimal(38, 6)
            ) AS CostPool
        FROM RunningPositions AS rp
        INNER JOIN OrderedTrades AS ot
            ON ot.InstrumentId = rp.InstrumentId
           AND ot.TradeNumber = rp.TradeNumber + 1
    ),
    -- Keep only the last running row per instrument (the final position).
    FinalPositions AS
    (
        SELECT
            rp.InstrumentId,
            rp.Isin,
            rp.Symbol,
            rp.InstrumentCurrency,
            rp.Quantity,
            rp.CostPool
        FROM RunningPositions AS rp
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM OrderedTrades AS nextTrade
            WHERE nextTrade.InstrumentId = rp.InstrumentId
              AND nextTrade.TradeNumber = rp.TradeNumber + 1
        )
          AND rp.Quantity <> 0
    ),
    -------------------------------------------------------------------------
    -- 5. Latest market price on or before @AsOfDate
    --    (one price per instrument per day is unique; we still need the latest day).
    -------------------------------------------------------------------------
    RankedPrices AS
    (
        SELECT
            mp.InstrumentId,
            mp.Price,
            mp.Currency,
            mp.AsOfDate,
            ROW_NUMBER() OVER
            (
                PARTITION BY mp.InstrumentId
                ORDER BY mp.AsOfDate DESC
            ) AS PriceNumber
        FROM dbo.MarketPrices AS mp
        INNER JOIN FinalPositions AS fp
            ON fp.InstrumentId = mp.InstrumentId
        WHERE mp.AsOfDate <= @AsOfDate
    ),
    LatestPrices AS
    (
        SELECT
            InstrumentId,
            Price,
            Currency
        FROM RankedPrices
        WHERE PriceNumber = 1
    )
    -------------------------------------------------------------------------
    -- 6. Save valued positions to a temp table.
    --    Needed because two result sets cannot both read the same CTE.
    --    ZAR / Rate = USD (never multiply).
    -------------------------------------------------------------------------
    SELECT
        fp.InstrumentId,
        fp.Isin,
        fp.Symbol,
        fp.Quantity,
        fp.InstrumentCurrency,
        fp.CostPool / NULLIF(fp.Quantity, 0) AS UnitCostNative,
        lp.Price AS MarketPriceNative,
        lp.Currency AS MarketPriceCurrency,
        CASE fp.InstrumentCurrency
            WHEN N'USD'
                THEN fp.CostPool / NULLIF(fp.Quantity, 0)
            WHEN N'ZAR'
                THEN
                    (fp.CostPool / NULLIF(fp.Quantity, 0))
                    /
                    NULLIF(fx.Rate, 0)
        END AS UnitCostUsd,
        CASE lp.Currency
            WHEN N'USD'
                THEN lp.Price
            WHEN N'ZAR'
                THEN lp.Price / NULLIF(fx.Rate, 0)
        END AS MarketPriceUsd,
        fx.Rate AS FxRateUsdZar
    INTO #PositionValued
    FROM FinalPositions AS fp
    INNER JOIN LatestPrices AS lp
        ON lp.InstrumentId = fp.InstrumentId
    OUTER APPLY
    (
        SELECT TOP (1)
            r.Rate
        FROM dbo.FxRates AS r
        WHERE r.BaseCurrency = N'USD'
          AND r.QuoteCurrency = N'ZAR'
          AND r.AsOfDate <= @AsOfDate
        ORDER BY r.AsOfDate DESC
    ) AS fx
    OPTION (MAXRECURSION 0);

    -------------------------------------------------------------------------
    -- 7–8. Cash: same versioning, convert to USD, group by currency.
    -------------------------------------------------------------------------
    ;WITH RankedCash AS
    (
        SELECT
            ce.ExternalRef,
            ce.Amount,
            ce.Currency,
            ce.AsOf,
            ce.ReceivedAt,
            ROW_NUMBER() OVER
            (
                PARTITION BY ce.ExternalRef
                ORDER BY
                    ce.AsOf DESC,
                    ce.ReceivedAt DESC
            ) AS VersionNumber
        FROM dbo.CashEvents AS ce
        WHERE ce.AccountId = @AccountId
          AND ce.IsApplied = 1
          AND ce.AsOf < @Cutoff
          AND ce.TradeDate <= @AsOfDate
    ),
    CurrentCash AS
    (
        SELECT
            ExternalRef,
            Amount,
            Currency
        FROM RankedCash
        WHERE VersionNumber = 1
    )
    SELECT
        cc.Currency,
        SUM(cc.Amount) AS Amount,
        SUM
        (
            CASE cc.Currency
                WHEN N'USD'
                    THEN cc.Amount
                WHEN N'ZAR'
                    THEN cc.Amount / NULLIF(fx.Rate, 0)
            END
        ) AS AmountUsd
    INTO #CashValued
    FROM CurrentCash AS cc
    OUTER APPLY
    (
        SELECT TOP (1)
            r.Rate
        FROM dbo.FxRates AS r
        WHERE r.BaseCurrency = N'USD'
          AND r.QuoteCurrency = N'ZAR'
          AND r.AsOfDate <= @AsOfDate
        ORDER BY r.AsOfDate DESC
    ) AS fx
    GROUP BY
        cc.Currency;

    -------------------------------------------------------------------------
    -- 9. Instrument-level result (quantity, unit cost, MV, unrealized P/L in USD).
    -------------------------------------------------------------------------
    SELECT
        pv.Isin,
        pv.Symbol,
        pv.Quantity,
        ROUND(pv.UnitCostUsd, 2) AS UnitCostUsd,
        ROUND(pv.MarketPriceNative, 2) AS MarketPriceNative,
        pv.MarketPriceCurrency AS InstrumentCurrency,
        ROUND(pv.MarketPriceUsd, 2) AS MarketPriceUsd,
        ROUND(pv.Quantity * pv.MarketPriceUsd, 2) AS MarketValueUsd,
        ROUND(
            (pv.Quantity * pv.MarketPriceUsd)
            -
            (pv.Quantity * pv.UnitCostUsd),
            2
        ) AS UnrealizedPnlUsd
    FROM #PositionValued AS pv
    ORDER BY
        pv.Symbol;

    -------------------------------------------------------------------------
    -- 10. Account-level result. TotalValueUsd = positions + cash.
    -------------------------------------------------------------------------
    SELECT
        @AccountCode AS AccountCode,
        @AsOfDate AS AsOfDate,
        N'USD' AS Currency,
        ROUND(
            ISNULL((SELECT SUM(Quantity * MarketPriceUsd) FROM #PositionValued), 0),
            2
        ) AS TotalMarketValueUsd,
        ROUND(
            ISNULL((SELECT SUM(AmountUsd) FROM #CashValued), 0),
            2
        ) AS CashTotalUsd,
        ROUND(
            ISNULL((SELECT SUM(Quantity * MarketPriceUsd) FROM #PositionValued), 0)
            +
            ISNULL((SELECT SUM(AmountUsd) FROM #CashValued), 0),
            2
        ) AS TotalValueUsd,
        (
            SELECT TOP (1)
                Rate
            FROM dbo.FxRates
            WHERE BaseCurrency = N'USD'
              AND QuoteCurrency = N'ZAR'
              AND AsOfDate <= @AsOfDate
            ORDER BY AsOfDate DESC
        ) AS FxRateUsdZar;
END;
GO
