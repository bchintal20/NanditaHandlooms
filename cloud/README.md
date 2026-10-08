# Azure version

The original local Python app remains intact. Azure deployment has not run yet.

- Website: Static Web Apps Free, Central US, managed .NET 10 isolated Functions.
- Database: Azure SQL free offer, pause at monthly allowance exhaustion.
- Owner login: Microsoft through Static Web Apps. Invite the owner into the `boutique-owner` role; backend additionally matches `BoutiqueOwnerEmail`.
- Public HTML contains no business records. APIs require platform authentication and owner authorization. All data remains behind the API.
- Required server settings: `BoutiqueSqlConnection`, `BoutiqueOwnerEmail`, `BoutiquePublicOrigin`.

## Local checks

Run from the repository root:

```powershell
python cloud/export_ledger.py
python cloud/build_ui.py
dotnet build cloud/Api/Boutique.Api.csproj
dotnet run --project cloud/Tests/Boutique.Tests.csproj -- .artifacts/migration-ledger.json --sql
dotnet publish cloud/Api/Boutique.Api.csproj -c Release -o .artifacts/api
```

Tests with `--sql` create and remove a uniquely named disposable database on localhost using Windows authentication. They never touch the boutique's source database. The local SQL certificate override is explicit and restricted to localhost/the computer's own name; production code always verifies certificates.

The export contains business data and stays in Git-ignored `.artifacts/`. Re-export immediately before actual migration. Do not place the export in the public site or compiled API package.

## Deployment preparation

Infrastructure parameters exclude secrets. Generate separate administrator and app passwords during deployment; keep them out of logs/source/output. Setup schema with `dotnet run --project cloud/Tools -- setup` and an administrator connection supplied through the process environment variable `BoutiqueSqlConnection`. Execute `cloud/sql/app-user.sql` using a parameterized `@password` command, then use the limited app connection for the Functions API. Configure only the exact temporary bootstrap client IP; remove that firewall rule after schema/import setup.

Import using `dotnet run --project cloud/Tools -- import .artifacts/migration-ledger.json`. The importer refuses a nonempty cloud database and verifies full normalized payload equivalence. Preserve existing historical-review records. Choose cloud as the active ledger after verification; there is no automatic local/cloud sync.

Only the four explicit files in `.artifacts/site/` and compiled artifacts in `.artifacts/api/` are deployable. Never publish the repository root. Initial import is an operator command, not an unauthenticated web endpoint.

## Recovery and cost

Every accepted save is transactional with revision checking and recovery history (last 50 saves). Restore requires the current revision. SQL free offer has seven-day point-in-time recovery; download JSON backups regularly. No keepalive or SQL health probe is configured; connections have pooling disabled to permit idle pause. Cold starts may require waiting and refreshing.

Target cost is $0 within allowances. Free SQL pauses until next month if limits are reached; Free SWA has no uptime SLA. No paid fallback is enabled. Database networking permits Azure services across tenants; strong credentials and restricted SQL grants still apply.
