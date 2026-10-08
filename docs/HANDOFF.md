# Future-chat handoff

Status recorded October 7, 2026 (America/Los_Angeles). This is a snapshot, not a substitute for inspecting the current repository, ledger, or Azure resources.

## Start here

1. Read [REQUIREMENTS.md](REQUIREMENTS.md) for the business purpose, formulas, import decisions, and boundaries.
2. Read [../README.md](../README.md) for local operation and [../cloud/README.md](../cloud/README.md) for cloud architecture, checks, and migration.
3. Inspect Git status and the relevant code before changing behavior. Preserve the owner's live data; inspect records read-only when establishing the current state.
4. For Azure work, read the active session and deployment checklist below, then verify the personal account in the same CLI process. Obtain deployment approval if it is still pending.

Repository: `C:\Users\BharathChintalapani\Documents\Repos\NanditaHandlooms`. Keep work here; the owner explicitly rejected a OneDrive location. The chat's generated working directory is not the application repository.

## What is complete

- Original responsive JavaScript UI and Python/SQLite local application, including inventory, shipment cost allocation, pricing, sales, customer shipping, full refunds, historical review, quarterly CSV, and backups.
- Separate C#/.NET 10 isolated Functions API, Azure SQL ledger store, migration tools, and restricted cloud frontend staging under `cloud/`.
- Bicep infrastructure under `infra/` for Static Web Apps Free with managed Functions and Azure SQL free offer in Central US.
- Local Git repository with source checkpoint `ee213d6`. No Git remote was configured and nothing was uploaded at that checkpoint. Documentation added afterward may be uncommitted; inspect current status.
- Validation at that checkpoint: API/tools builds and publish succeeded, 24 C# security/validation/SQL checks passed, financial regression tests passed against the cloud UI, desktop/mobile UI checks passed, and Bicep/conformance checks passed.

**Azure resources and code are deployed; owner access verification is in progress.** The owner explicitly approved deployment after documentation. The HTTPS homepage and integrated API health return 200; unauthenticated data access and forged owner headers return 401. SQL schema, restricted user, full-payload source import, restricted grants, transactional rollback and stale-revision rejection were verified. The temporary operator firewall rule was removed. The source ledger remains intact.

Website: https://gray-grass-0cab2cd10.6.azurestaticapps.net/

The first invitation was rejected because the browser signed in as the owner's Aya work account rather than the intended personal Microsoft account. Personal sign-in subsequently reached the app but owner-role verification is still pending. Keep the configured personal-owner email restriction; do not authorize the work identity to bypass that error. The local/cloud active-ledger decision is pending verification.

## Architecture and code map

| Location | Purpose |
| --- | --- |
| `app.js`, `index.html`, `style.css` | Original responsive UI and financial calculations. |
| `server.py` | Original local authentication, validation, SQLite persistence, revision protection. |
| `cloud/Core/` | Cloud identity checks, equivalent ledger validation, transactional SQL state/history. |
| `cloud/Api/` | Authenticated state/restore endpoints; database-free health endpoint. |
| `cloud/Tools/`, `cloud/sql/` | Operator-only schema setup, limited SQL user, initial import. |
| `cloud/build_ui.py` | Stages explicit public assets and cloud login behavior. |
| `cloud/export_ledger.py` | Read-only SQLite export; private output under ignored `.artifacts/`. |
| `cloud/Tests/`, `cloud/test_ui.cjs`, `test_calculations.js` | Backend, SQL integration, UI, and financial verification. |
| `infra/` | Source-controlled Bicep and nonsecret deployment parameters. |

Cloud authorization combines a trusted Static Web Apps Microsoft identity, `boutique-owner` role, and configured owner email. Public HTML contains no business records. The API validates origin on writes and limits request size. SQL saves are transactional with revision conflicts and the last 50 states retained; production SQL certificate verification remains enabled.

## Data and recovery

Live local database: `data/boutique.sqlite3`; previous-state backup: `data/last-good-backup.json`. These files are Git-ignored. Do not recreate the database from source or assume a clone contains inventory. Download full JSON backups before migrations or historical-cost changes.

Last read-only local snapshot before cloud preparation: revision 4, 22 product batches, 4 shipments, 12 sale entries, 0 returns, and **8 pending historical-review records**. There were 58 received pieces, 15 pieces in recorded sales, and $1,871.87 recorded receipts. These counts are historical context; query the current ledger before asserting they remain true. Pending-record count is not a piece count and records may have zero remaining quantity.

Re-export immediately before migration. Initial cloud import must refuse an existing nonempty ledger and verify normalized full-payload equivalence, including pending reviews and shipping fields. Keep the original local data. After verification, establish which deployment is the active ledger; saves in one do not synchronize to the other.

## Phone access history

Local phone access repeatedly failed despite explicit HTTP and port 8765. A 502 page from nginx was seen. The laptop and phone are company managed; a management profile was confirmed on the phone and company firewall policy prevented local firewall rules. Treat this as an operational constraint, not proof every browser error had the same cause. Respect managed-device restrictions. The selected next approach is public HTTPS hosting, which removes the dependency on reaching the laptop over the LAN. Hosted phone access remains unverified until deployment.

## Azure account and planned resources

Use only the personal CLI configuration. The default CLI account belongs to the employer.

```powershell
$env:AZURE_CONFIG_DIR = 'C:\Users\BharathChintalapani\Documents\Repos\NanditaHandlooms\.azure-personal'
```

Set it before **every** Azure CLI process, including reads, and verify the account before mutations. Never print access tokens, passwords, connection strings, or deployment tokens.

| Setting | Planned/verified value |
| --- | --- |
| Personal subscription | Azure subscription 1 |
| Subscription ID | `ef6358bf-6180-4a31-a0e2-97b0a66dc438` |
| Tenant ID | `99fe16c7-0092-41c3-8ca5-a7e3f7031788` |
| Owner login | `bharath.chintalapani@gmail.com` |
| Region | `centralus` |
| Resource group | `rg-nandita-dev-cbbf` |
| Website | `swa-nandita-dev-cbbf`, Static Web Apps Free |
| SQL | `sql-nandita-dev-cbbf` / `nandita`, `GP_S_Gen5_2` |
| Cost behavior | Target $0 within allowances; SQL `useFreeLimit=true`, exhaustion `AutoPause`; no paid fallback. |

Central US SQL provisioning capability and free quota were available during preparation; westus2/eastus2 were provisioning restricted. Recheck availability before deploying. SWA Central US support was confirmed but its subscription quota limit was not exposed. Free-trial/account terms and service allowances can change; verify current terms rather than promising permanent free hosting.

No standalone Functions host, Key Vault, storage account, managed identity, or paid telemetry is planned. SQL networking includes the disclosed Azure-services exception and optional exact temporary bootstrap IP, with strong credentials and restricted SQL grants. Explicitly remove the bootstrap firewall rule after setup; omitting a conditional resource in a later incremental deployment does not delete an existing rule.

## Deployment handoff and remaining work

Active session ID: `cbbfbb70-26b5-4ef5-9676-9e0ebc0213e3`, under `.copilot-azure/sessions/`. The active pointer, context, prepare plan, scaffold manifest, review, deploy checklist, final deployment result, audit and summary are private and Git-ignored. The deployment attempt is recorded as a partial handoff: resources/code and SQL import succeeded, but refreshed personal-owner API access remains unverified. Resume that authorization check before treating the app as fully usable; do not provision replacement resources. These artifacts exist on this computer but will not be present in a fresh clone.

The applied skill is `C:\Users\BharathChintalapani\.agents\skills\azure-app-onboard\SKILL.md`. For deployment, read its embedded `deploy\SKILL.md` and this session's `deploy-checklist.md` before any deployment command, and again after compaction. Scaffold approval does not grant deployment approval. Do not substitute an azd workflow; this project uses Bicep and token-based local Static Web Apps deployment.

After explicit deployment approval:

1. Recheck subscription, plan, runtime support, and deployment preflight/what-if. Bicep emitted BCP081 warnings because local SWA schema types were unavailable; compilation alone does not prove live compatibility.
2. Generate protected, separate SQL administrator/app credentials; preserve them across retries. Supply secure parameters through a protected file, never source control or logs.
3. Provision the approved free resources and verify actual SKU, region, tags, TLS, and free-limit exhaustion behavior.
4. Bootstrap SQL schema and restricted app user; import a fresh verified source export. Remove temporary operator firewall access.
5. Deploy only `.artifacts/site/` public assets and `.artifacts/api/` compiled API. Never publish the repository root or migration export.
6. Invite the owner into `boutique-owner`, verify the trusted identity's actual email/userDetails, and test unauthorized access, writes, revision conflicts, health, and phone access over HTTPS.
7. Record real URLs, resource IDs, checks, and limitations in the session result and update this document's status. Choose the active ledger with the owner and retain backups.

Live .NET 10 health/routing, least-privilege SQL grants, migration equivalence, TLS1.2, seven-day PITR, and free-limit AutoPause configuration are now verified. The owner confirmed the personal Gmail identity, and Azure returned a successful boutique-owner assignment. Refreshed owner read/write and phone access remain unverified. Local test coverage used direct endpoint calls, a local SQL test database, and a mocked read-only UI API; live checks so far include public HTTP/access denial and direct encrypted SQL verification, not a completed authenticated browser end-to-end test.

## Keeping this handoff useful

Update REQUIREMENTS.md when business rules change. Update this file when implementation, deployment, data location, or unresolved work changes. Use the code and current records as evidence of implemented behavior; label proposed work explicitly. Keep private record exports and secrets out of documentation and Git.

Suggested prompt for a new chat:

> Continue work on Nandita Handlooms in C:\Users\BharathChintalapani\Documents\Repos\NanditaHandlooms. Read AGENTS.md, docs/REQUIREMENTS.md, and docs/HANDOFF.md first, then inspect the current repository. My next task is: [describe task].
