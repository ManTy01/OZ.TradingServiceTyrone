USE TradeIngestion;
GO

-- Task 2 supporting DDL. EF migrations already created these; this file documents them.
-- Access path for the proc: seek one account, AsOf < cutoff, applied rows only.
-- INCLUDE covers quantity/price/side so the engine need not look up the clustered PK.
-- TradeDate <= @AsOfDate is a residual predicate (second range on the same index is not useful).

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TradeEvents_Account_AsOf_Applied')
BEGIN
    CREATE INDEX IX_TradeEvents_Account_AsOf_Applied
    ON dbo.TradeEvents (AccountId, AsOf)
    INCLUDE (ExternalRef, InstrumentId, Side, Quantity, Price, Currency, TradeDate, IsApplied, Decision)
    WHERE [IsApplied] = 1;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TradeEvents_ExternalRef_AsOf')
BEGIN
    CREATE INDEX IX_TradeEvents_ExternalRef_AsOf
    ON dbo.TradeEvents (ExternalRef, AsOf);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashEvents_Account_AsOf_Applied')
BEGIN
    CREATE INDEX IX_CashEvents_Account_AsOf_Applied
    ON dbo.CashEvents (AccountId, AsOf)
    INCLUDE (ExternalRef, [Type], Amount, Currency, TradeDate, IsApplied, Decision)
    WHERE [IsApplied] = 1;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CashEvents_ExternalRef_AsOf')
BEGIN
    CREATE INDEX IX_CashEvents_ExternalRef_AsOf
    ON dbo.CashEvents (ExternalRef, AsOf);
END
GO

-- Latest price / FX on or before @AsOfDate: unique on (instrument/pair, date) is enough.
-- Existing unique indexes from EF:
--   MarketPrices (InstrumentId, AsOfDate)
--   FxRates (BaseCurrency, QuoteCurrency, AsOfDate)
--   Accounts (AccountCode)