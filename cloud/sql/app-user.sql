-- Execute with a generated app password through SqlCommand parameters (@password).
-- Schema setup runs separately under the operator account.
IF USER_ID('boutique_app') IS NULL
BEGIN
    DECLARE @statement nvarchar(max) = N'CREATE USER [boutique_app] WITH PASSWORD = ' + QUOTENAME(@password, '''') + N';';
    EXEC sys.sp_executesql @statement;
END;
ELSE
BEGIN
    DECLARE @rotation nvarchar(max) = N'ALTER USER [boutique_app] WITH PASSWORD = ' + QUOTENAME(@password, '''') + N';';
    EXEC sys.sp_executesql @rotation;
END;
GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.BoutiqueLedger TO boutique_app;
GRANT SELECT, INSERT, DELETE ON dbo.BoutiqueHistory TO boutique_app;
