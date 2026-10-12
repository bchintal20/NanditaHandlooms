# Start here: future chat handoff

## Current status

The application is implemented and deployed at [Nandita Handlooms](https://gray-grass-0cab2cd10.6.azurestaticapps.net/). The original Microsoft-access deployment was owner-confirmed. Hosted access was replaced on October 12, 2026 with invite-only boutique email/password accounts; the account release is deployed and its anonymous/forged-session/origin gates are verified. The owner confirmed successful sign-in using the new password. Authenticated inventory display was not separately stated in that confirmation. After the image-heavy loading fix, the owner also confirmed that the website loaded on phone Chrome. This replaces earlier notes saying phone confirmation was pending.

Repository: `C:\Users\BharathChintalapani\Documents\Repos\NanditaHandlooms`. Keep it outside OneDrive. Cloud is the active shared ledger; local SQLite is a separate copy, with no synchronization. Do not import the old local snapshot over live cloud records.

Source is tracked in Git with origin `https://github.com/bchintal20/NanditaHandlooms.git`, branch `master`. The October 12, 2026 source checkpoint includes variants, labels/media, loading and lazy-photo fixes, regression tests, and consolidated documentation. Inspect actual Git status and diffs before resuming; deployment and commits are separate operations. Git excludes business data and credentials.

## Resume a task

1. Read root AGENTS.md and inspect the working tree. Completion: identify existing changes and preserve them.
2. Read the documents relevant to the task using the table below. Completion: understand affected business rules, persistence, and operational limits.
3. Implement using separate test fixtures. Completion: appropriate checks pass and live records are preserved.
4. Update the relevant reference document and this current-status section if needed. Add release evidence to CHANGELOG. Deploy only within the user's authorized scope, following Operations.

| Document | Authority |
| --- | --- |
| [REQUIREMENTS](REQUIREMENTS.md) | Owner decisions and financial/inventory/reporting behavior. |
| [FEATURES](FEATURES.md) | Screen workflows and supported capabilities. |
| [ACCOUNTS](ACCOUNTS.md) | Account workflows, session security, and recovery procedures. |
| [ARCHITECTURE](ARCHITECTURE.md) | Source map, schema, APIs, photos, and access controls. |
| [OPERATIONS](OPERATIONS.md) | Startup, backups, tests, personal Azure profile, deployment, troubleshooting. |
| [CHANGELOG](CHANGELOG.md) | Historical milestones and verification; not instructions to repeat setup. |

## What is built

Overview; purchase-batch inventory; shared-cost color variants; landed-cost shipment allocation; adjustable margin/markup MSRP; bold rounded tax-inclusive .99 price; product/variant images and Instagram previews; downloadable/printable price labels; manually recorded sales/payment labels; customer shipping income and expense; full refunds and optional restocking; workbook Historical review; quarterly tax-preparation CSV; settings; full JSON backup/restore; revision conflicts; local access code; private boutique accounts, remembered sessions, recovery codes, and owner-managed invitations/reset links.

Cloud startup now retrieves metadata separately from photos. Inventory photos are private lazy requests; backups still contain actual JPEG data. No Blob storage or database migration was introduced. The fix improves a concrete large-response bottleneck, but cold starts and free database limits can still cause delays.

## Decisions to preserve

- Shared parent costs and quantity: colors divide existing stock, never create additional received pieces.
- Default 50% gross margin; explicit markup mode exists because the original workbook used markup.
- Actual collected tax is excluded from revenue/profit. All payment methods, including cash, are included in reporting.
- Customer shipping income is distinct from actual postage expense. Unknown expense stays unknown.
- Full backups contain photos; metadata-only browser state is not a restorable backup.
- A stale revision is a conflict, never permission to overwrite another device's changes.
- Target hosting cost is $0 within configured free allowances; infrastructure has no automatic paid fallback.

## Remaining limits and follow-up

- SUPVAN T50M Pro physical printing and installed roll dimensions have not been confirmed. Default label preview is 40 × 30 mm; owner can change it. Software PNG/PDF behavior was tested.
- Instagram embeds depend on public-post availability and Instagram restrictions. Real-world embed availability is not guaranteed; uploaded photos are dependable.
- New lazy-photo save behavior has isolated tests and a read-only live roundtrip check. Phone loading is owner-confirmed; no live business transaction was written just to test the release.
- Actual customer postage can remain missing; profit is flagged incomplete until supplied.
- General sale editing, partial refunds/exchanges, arbitrary stock adjustments, payment integrations, granular staff permissions, automated tax filing, offline phone mode, and automatic local/cloud sync are outside current scope.
- Tests currently depend in part on ignored private fixtures and the builder computer's Playwright runtime. A clean clone requires fixtures/runtime setup; synthetic fixture portability is future maintenance work.
- Full encoded ledger limit remains 15 MB. Lazy transport reduces page-load size, not stored photos or whole-ledger save costs. Consider separately stored images if the catalog outgrows this design.

## Copy into a new chat

```text
Continue work on Nandita Handlooms at:
C:\Users\BharathChintalapani\Documents\Repos\NanditaHandlooms
Read AGENTS.md and docs/HANDOFF.md, then the linked docs relevant to the task.
Preserve existing working-tree changes and live business data. Cloud is the active
ledger; local data is separate. Use the personal Azure profile for any Azure action.
My next task is: [describe the feature or maintenance work].
```

## Boutique account release

Deployed October 12, 2026. Read [ACCOUNTS.md](ACCOUNTS.md) for usage, protections, schema, APIs, migration, and recovery. One owner manages up to 20 accounts; members share full ledger access. No new paid service or email provider was added. Additive schema migration verified the financial revision/payload unchanged; temporary SQL operator access was removed. Account checks (48 including isolated SQL), ledger/API checks (50), desktop/mobile account flows, and existing financial/photo/variant/label/startup regressions passed. Live assets match stage; anonymous ledger/backup/photo/account requests and forged cookies return 401; cross-origin login returns 403. Private owner setup token was saved in ignored artifacts and opened in Chrome. The owner subsequently confirmed: “I was able to login with new password.” Password setup and sign-in are owner-confirmed; recovery-code storage and authenticated inventory display were not separately confirmed. Never ask the owner to paste a password or recovery code into chat.

The account release source, tests, and documentation are included in the next source checkpoint after 8455275. Inspect actual Git status and remote tracking before resuming; source commits and Azure deployments are separate operations. Private business data and account setup credentials are excluded from Git.
