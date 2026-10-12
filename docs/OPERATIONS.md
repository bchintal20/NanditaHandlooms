# Running, recovery, validation, and deployment

All commands below run from `C:\Users\BharathChintalapani\Documents\Repos\NanditaHandlooms` unless stated otherwise. Inspect the current working tree before modifying files. Use [Architecture](ARCHITECTURE.md) for storage/API details and [Handoff](HANDOFF.md) for current status.

## Local startup

Double-click **Start Boutique.bat**, or run:

```powershell
python server.py
```

Open `http://localhost:8765/` and use the code printed in the console. The code file is `data/access-code.txt`; it can be changed while stopped. Keep the server window open. `--no-browser` suppresses automatic browser launch.

For LAN phones stop the existing instance, then use **Start Boutique WiFi.bat** or `python server.py --wifi`. Both devices must share trusted private Wi-Fi; use the actual printed computer IP **and port**. Allow Python only on Private Windows Firewall networks if needed. The computer must stay awake. Guest networks and company management can prevent access; do not bypass employer policy or expose this server through router port forwarding.

Port occupied: open the existing instance or stop it using **Stop Boutique.bat** before restarting. A home-screen shortcut is a convenience, not offline phone storage.

## Data and backups

Cloud is the active ledger. Local `data/boutique.sqlite3` is separate and can be stale. Avoid concurrent manual edits to these copies; no sync/merge is implemented.

1. In the active app choose Settings & backup → download full JSON backup. Completion: the file parses as JSON and includes actual JPEG data for attached photos, not `/api/photo` URLs or `lazyPhotos` metadata.
2. Store it privately outside the source repository/public assets. Git-ignored storage is not a substitute for an independent backup.
3. Before restore, download a fresh backup of the current ledger and verify which deployment is active. Restore replaces the whole ledger and checks revision; do not use it to fix a loading issue or deploy code.

Local previous-state recovery is `data/last-good-backup.json`. For a full local filesystem backup, stop the app and copy its `data` directory to private backup storage. Cloud maintains last-50-save transactional history and configured seven-day SQL point-in-time recovery. Download backups regularly rather than relying only on these rolling windows. Keep quarterly CSVs used for filed returns; later cost edits can change historical profit.

## Validation

For documentation-only work, validate links and factual claims against source and run `git diff --check`. For code changes choose checks matching affected behavior:

```powershell
node --check app.js
python -m unittest test_server
node test_calculations.js
dotnet build cloud/Api/Boutique.Api.csproj
dotnet run --project cloud/Tests/Boutique.Tests.csproj -- .artifacts/migration-ledger.json
python cloud/build_ui.py
node cloud/test_ui.cjs
node test_variants_ui.cjs
node test_labels_ui.cjs
node test_startup_ui.cjs
node test_lazy_photos_ui.cjs
git diff --check
```

Prerequisites: Python, Node.js, .NET 10 SDK for cloud code, and Playwright for browser checks. Existing browser tests resolve Playwright from the builder computer's bundled Codex runtime and launch installed Edge. Check imports before running on another machine. Some tests read Git-ignored `initial-workbook-import.json` or `.artifacts/migration-ledger.json`; these are private fixtures, absent from a clean clone. Cloud tests need a ledger containing appropriate recorded-sale cases, not an empty initial seed. Tests use temporary databases/mocked HTTP; do not substitute live cloud connection strings.

`cloud/export_ledger.py` creates a private export of **local** data. It does not fetch the active cloud ledger. Existing local export can be a test fixture but must never be mistaken for current production data.

The optional cloud test `--sql` mode creates and drops a uniquely named disposable local SQL Server database. It is not a routine production check; use only an explicitly isolated local environment. Default tests above avoid that mode. Label tests can use `BOUTIQUE_SITE_ROOT=.artifacts/site` to exercise staged CSP.

## Personal Azure environment

| Item | Value |
| --- | --- |
| Website | `https://gray-grass-0cab2cd10.6.azurestaticapps.net/` |
| Personal owner | `bharath.chintalapani@gmail.com` |
| Subscription | `ef6358bf-6180-4a31-a0e2-97b0a66dc438` |
| Tenant | `99fe16c7-0092-41c3-8ca5-a7e3f7031788` |
| Resource group | `rg-nandita-dev-cbbf` |
| Static Web App | `swa-nandita-dev-cbbf` |
| SQL server / database | `sql-nandita-dev-cbbf` / `nandita` |

Before every CLI session set the personal profile and verify subscription/tenant:

```powershell
$env:AZURE_CONFIG_DIR = 'C:\Users\BharathChintalapani\Documents\Repos\NanditaHandlooms\.azure-personal'
az account show --query '{subscription:id,tenant:tenantId}' -o json
```

The default CLI profile belongs to the employer. If authentication expires, sign in explicitly with the personal account and this tenant, then repeat the check. Browser account selection can choose the work identity silently; use an explicit account picker/personal profile. Regular Chrome worked during the last deployment. Device-code login previously hit a security-policy error; diagnose the actual error rather than treating disabled MFA as a deployment prerequisite. Final personal-account MFA settings were not independently inspected.

SWA Free + integrated Functions and SQL free offer are configured for a $0 target within allowances. SQL uses `useFreeLimit=true` and `freeLimitExhaustionBehavior=AutoPause`; exhaustion can pause service until the allowance resets. No paid fallback or keepalive is configured. Recheck actual subscription usage/pricing before infrastructure changes. API health is worker-only and should not continuously wake SQL.

## Routine code deployment

Run appropriate isolated tests first, then, when deployment is authorized:

```powershell
pwsh -File cloud/redeploy.ps1
```

Windows PowerShell alternative: `powershell -ExecutionPolicy Bypass -File cloud/redeploy.ps1`.

The script verifies the personal subscription/tenant, stages four public UI files, publishes the .NET API, uses SWA CLI 2.0.10 to upload production code, obtains the deployment token without printing it, clears the process token, and cleans its verified temporary staging path. It does not provision resources, change schema, or import business records.

Completion checks: deployment exits successfully; staged assets match live assets; `/api/health` responds; anonymous state/backup/photo access remains blocked; the authorized owner loads inventory. Health alone cannot confirm SQL or owner access. Use isolated tests rather than creating fake production sales. The CLI has emitted an obsolete .NET 10 schema warning despite successful upload; an upload failure still requires investigation.

Tokens, connection strings, invitation links, account caches, private exports, and logs remain in ignored local locations. Never print secrets or copy them into docs. Never publish the repository root.

## Initial setup is not a redeployment step

Infrastructure source is under `infra/`; setup/import tooling is under `cloud/Tools` and `cloud/sql`. Initial provisioning, restricted application-user creation, empty-ledger import, and owner invitation were already completed. Repeat only for a deliberately new environment with a reviewed plan. The importer rejects nonempty databases. Comparing current cloud records to an old local migration fixture is not a valid ongoing integrity check.

Direct SQL operator diagnostics may need a temporary exact-client-IP firewall rule. Remove it after use. The configured Azure-services firewall exception remains; SQL credentials and grants still protect access. There is no broad operator-IP rule from the photo investigation left in place.

## Troubleshooting

| Symptom | Check / action |
| --- | --- |
| Local phone 502, blank page, secure-connection error | Verify exact `http://computer-IP:8765/`, running WiFi-mode server, same private network, firewall and management restrictions. An nginx 502 page alone does not identify the Python app. |
| Hosted account denied | Check `/.auth/me` in the same browser. Verify personal identity and `boutique-owner` role; never paste full identity payload/tokens into public logs. A valid fresh invitation may need redemption with that account. |
| Invalid owner invitation | Generate/redeem a fresh invitation through the authorized owner setup workflow; invitation links are private and expire. CLI user-list output was not reliable proof of successful redemption during setup. |
| Loading / retrying | Check staged/live script fingerprint, then distinguish API worker health from authenticated state request. Examine SQL status/free limits and transient startup. Owner now confirmed the photo fix loads on phone. |
| Many attached photos | Hosted UI must use lazy state + private images; backups still fetch full data. Confirm old JavaScript is not cached. Keep an eye on the full stored 15-MB limit. |
| Save conflict | Refresh, reopen the form, reapply deliberate changes against the latest revision. Do not force an old snapshot over newer data. |
| Missing Instagram image | Check public post/reel URL; use uploaded photo when Instagram restricts embeds. |
| Label size/cropping | Set dimensions matching roll; check SUPVAN app image import, scale, margins, and copies. Physical output remains owner verification. |
| Profit seems too high | Verify postage is known, shipment costs are actual, tax excluded, shared shipping entered only once, and unresolved historical receipts reviewed. |

If persistent hosted failures need diagnosis, gather status/timing and sanitized errors without exporting inventory into logs. Record what was actually verified and what remains uncertain in the handoff/history.
