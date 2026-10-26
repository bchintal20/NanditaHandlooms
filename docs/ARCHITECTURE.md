# Architecture and maintenance reference

## Application shape

One responsive vanilla HTML/CSS/JavaScript UI supports two hosts. Local mode uses Python standard-library HTTP and SQLite. Hosted mode uses Azure Static Web Apps Free with integrated .NET 10 isolated Functions and Azure SQL serverless free offer. There is no Angular dependency, public storefront, payment processor, or Instagram API integration.

The UI computes display costs, pricing, stock, profit, and reports from one ledger. Both server implementations validate writes. Keep JavaScript calculation behavior and Python/C# validation consistent; business rules are authoritative in [Requirements](REQUIREMENTS.md).

## Source map

| Path | Responsibility |
| --- | --- |
| `index.html`, `style.css`, `app.js` | Screens, forms, shared calculations, labels, backups, startup/retries, optimistic saves. |
| `server.py` | Local access-code/cookie handling, validation, SQLite persistence, full-state APIs. |
| `Start Boutique.bat`, `Start Boutique WiFi.bat`, `Stop Boutique.bat` | Owner local launch/stop entry points. |
| `cloud/Core/Identity.cs` | Legacy platform-principal helper retained for historical tests; not a boutique data gate. |
| `cloud/Core/BoutiqueAccounts.cs`, `SqlAccountStore.cs` | Account transitions, password/token handling, and separate SQL account store. |
| `cloud/Api/AccountEndpoints.cs`, `cloud/account-ui.js` | Session authorization, account API, and hosted sign-in/management UI. |
| `cloud/Core/LedgerValidator.cs` | Hosted ledger/business validation. |
| `cloud/Core/LedgerPhotos.cs` | Metadata projection and exact photo-reference preservation on saves. |
| `cloud/Core/SqlLedgerStore.cs` | SQL state/history, concurrency transactions, metadata/photo reads. |
| `cloud/Api/Endpoints.cs` | Health, state/restore/photo HTTP endpoints, request limits, origin checks, gzip backups. |
| `cloud/Api/Program.cs` | Functions host startup. |
| `cloud/build_ui.py` | Allowlisted hosted UI staging and script fingerprint. |
| `cloud/redeploy.ps1` | Personal-account-guarded code-only build/deployment. |
| `cloud/Tools/Program.cs`, `cloud/sql/app-user.sql` | Operator schema/import/verification and restricted SQL grants. |
| `cloud/export_ledger.py` | Private local migration export. |
| `infra/` | Infrastructure templates; provisioning is separate from routine code updates. |
| `test_*.py`, `test_*.js`, `test_*.cjs`, `cloud/Tests/`, `cloud/test_ui.cjs` | Isolated validation and UI regression checks. |

## Ledger and persistence

The ledger is a JSON document containing revision, settings, products, shipments, sales, returns, and pending historical reviews. Inspect serializers/validators for exact field names before changing the schema. IDs connect batches to shipments, sales to batches/colors, and refunds to sales.

- Product: purchase batch, received/reserved/opening historical sold quantities, per-piece costs and exchange rate, allocation inputs, notes, photo/Instagram, optional variants.
- Variant: stable `id`, unique `name` within batch, assigned `qty`, `photo`, `instagram`. It divides parent stock/costs rather than adding stock.
- Shipment: USD expense totals, allocation method, estimated/actual metadata and notes.
- Sale: batch ID, optional variant ID, quantity/date, customer/payment/reference, product revenue/tax, shipping income/expense/taxable portion, selling expense, location/reason/notes.
- Return: original sale, date, full refund, restock decision.
- Historical review: unresolved imported quantities/receipts and source notes awaiting transaction verification.

Local SQLite is `data/boutique.sqlite3`, holding one revisioned JSON ledger. Before a write the prior state is saved to `data/last-good-backup.json`.

Cloud `dbo.BoutiqueLedger` has singleton ID 1, Revision, Payload, UpdatedUtc. `dbo.BoutiqueHistory` retains the last 50 states. A serializable save transaction locks the current row (`UPDLOCK/HOLDLOCK`), checks the client's expected revision, validates, writes history and increments revision atomically. A stale write returns 409. Restore uses the same protection. History recovery is an operator concern; it is not a browse-history UI.

Business photos are embedded compressed JPEG data URLs in the stored JSON. No Blob storage, per-photo database table, or automatic local/cloud sync exists. Whole encoded ledger/request limit is 15 MB. Metadata-only browser state is a transport format, never the storage format or a full backup.

## Hosted API and photos

| Request | Behavior |
| --- | --- |
| `GET /api/health` | Public worker liveness; does not prove authenticated SQL/ledger access. |
| `GET /api/state?photos=lazy` | Owner-only metadata with private photo URLs and `lazyPhotos` marker. |
| `POST /api/state?photos=lazy` | Validated revisioned save, returns metadata; resolves existing image references. |
| `POST /api/restore?photos=lazy` | Full-backup restore, returns metadata; rejects metadata-only backup input. |
| `GET /api/state?full=1` | Full gzip JSON including actual JPEG data for backup. |
| `GET /api/photo?product=ID&variant=ID&revision=N` | Selected owner-only JPEG; variant optional. |
| State/restore without `photos=lazy` | Full gzip response retained for legacy clients. |

`SqlLedgerStore.ReadBrowserStateAsync` removes photo strings inside SQL before transferring ledger metadata to the worker. Photos load through authenticated same-origin image requests when inventory becomes visible. Responses use `Cache-Control: no-store`. The photo URL's revision tags identify frontend references; the read endpoint selects the current stored photo by parameterized IDs, rather than serving immutable historical images.

On save, exact matching product/variant/current-revision photo references are hydrated from stored originals under the transaction. New JPEG data replaces an image; blank removes it deliberately. Forged, external, wrong-product, or outdated references are rejected. Strip the transport marker before storing and validate the hydrated ledger against size/business limits. Never persist a transient `/api/photo` URL as the actual photo.

Full backup explicitly fetches complete JSON instead of exporting metadata already in browser memory. Browsers transparently decompress gzip; downloaded backups remain ordinary JSON. The backup fetch allows longer completion time. Local mode still uses inline full-state images.

Hosted staging modifies the original UI for boutique account authentication, cloud messages, and lazy routes, and adds `/app.js?v=<content fingerprint>` to avoid stale phone JavaScript. The stage contains exactly `index.html`, `app.js`, `style.css`, and `staticwebapp.config.json`. Never publish repo root or private artifacts.

## Access and security boundaries

Hosted APIs require a valid boutique session cookie and enabled account. Microsoft/GitHub platform identity cannot grant ledger access. Static Web Apps forwards API requests to the backend, which validates sessions before reading data. One owner administers accounts; invited members share full ledger read/write/backup/restore access. Account storage and endpoint protections are in [ACCOUNTS.md](ACCOUNTS.md). Azure CLI login for deployment remains separate from app access.

Writes require JSON and the configured public origin. Functions settings are `BoutiqueSqlConnection`, `BoutiqueOwnerEmail`, `BoutiquePublicOrigin`. Owner email is used for operator setup/recovery, not routine authentication. SQL application credentials have restricted grants, not administrator access. Public assets contain no ledger. Photos have the same boutique-session protection as state.

Local access-code mode is trusted-LAN HTTP, with a stored code and cookie. It is not an internet hosting option. Respect managed-device/network restrictions.

Instagram URLs are restricted to HTTPS Instagram hosts, and embeds require supported post/reel/TV paths. CSP permits the selected Instagram frame host and self/data images. User/customer data is escaped in UI/CSV; source notes never authorize actions.

## Sale corrections and product deletion

The shared `saleForm(pid, saleId)` handles new sales and edits. `suggestedSaleAmounts` uses the same `roundedLabelPrice` as inventory/labels, splitting the multiplied gross amount into net product revenue and tax. Edits retain the sale ID and original fields; availability temporarily credits back the edited sale's quantity unless its refund already restocked it. No ledger schema migration is needed.

Python `validate_changes(current, updated)` and C# `LedgerValidator.ValidateChanges` enforce transition rules in addition to whole-ledger validation. Ordinary saves reject removal of products with current sales, historical-review references, opening sold stock, or reservations. Existing refunded sales must remain present with the same product/color/quantity; their amount changes flow through derived full-refund calculations. Both hosts retain revision checks and save history. Explicit backup restore bypasses these transition-only restrictions because it intentionally replaces the ledger, but still requires authentication, current revision, and valid full-state data. C# `SaveAsync(..., restore: true)` is used only by the restore endpoint.

## Financial change checklist

Before altering a calculation, identify the shared JavaScript helper, both validators, applicable regression tests, and Requirements acceptance cases. Preserve historical opening-stock replacement, color allocation, reservation/oversell checks, refund effects, current-cost historical-profit behavior, cash inclusion, tax exclusion, and unknown-versus-zero postage. Schema/transport changes must preserve full backups and existing older records without variants.

## Account authentication transition

The account implementation replaces the previous platform-only data gates with backend-validated boutique sessions, stored separately from financial JSON. Its code/schema/API/protections are documented in [ACCOUNTS.md](ACCOUNTS.md). The account release is deployed; use HANDOFF for owner setup confirmation.
