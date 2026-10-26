# Start here: future chat handoff

## October 26, 2026 release

Implemented sale editing, automatic .99 tax-inclusive sale defaults, and confirmed deletion of unused inventory, with matching Python/C# protections. The owner tested the local changes, confirmed they look good, and authorized committing, pushing, and deploying on October 26. Release execution is in progress; update this status with deployment evidence when complete.

New sales split the rounded label total into product revenue/tax; existing sale edits preserve actual amounts. Fully refunded sales keep batch/color/quantity fixed, with amount corrections affecting derived refunds. Inventory deletion is blocked by sales, historical references/opening sold quantity, and reservations. Shared-shipment deletion can reallocate costs. Details are in Requirements, Features, and Architecture.

Verification: synthetic sale-maintenance browser checks passed at desktop and phone widths; existing variant/photo/oversell checks passed; Python suite passed 8 tests; C# ledger suite passed 55 checks. These tests use isolated data and do not modify active business records. Use Operations for local testing. Local owner acceptance is confirmed. Cloud deployment verification remains pending.

## Deployed baseline

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
- Partial refunds/exchanges, arbitrary stock adjustments, payment integrations, granular staff permissions, automated tax filing, offline phone mode, and automatic local/cloud sync are outside current scope.
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

## Inventory layout fix — local and Azure

Fixed shared inventory CSS/markup so variant strips and long text do not widen grid columns. Preview areas and column widths are consistent. A subsequent compactness correction lets each tile fit its own content and places actions directly below it, eliminating empty space on cards without variants. Verified desktop/tablet/phone widths with mixed media, long text, and ten variants; labels/variant flows still pass. The owner confirmed the local layout looks good. The same fix is now deployed to Azure; live HTML/JavaScript/CSS match staging and anonymous account/data gates remain protected. Refresh either app to load the change. The inventory layout is included with the settings-feedback/global-logout source checkpoint; verify actual Git remote tracking when resuming.

## Defaults confirmation and global logout

Owner authorized implementation, Azure deployment, and GitHub push. Added durable inline saving/success/unsaved/error feedback and header Log out in both modes. Isolated desktop/mobile feedback/logout and local session-revocation checks pass. Deployed to Azure and verified live assets match staging, account/data endpoints remain protected, and the local server was restarted with logout support. Source checkpoint includes this feature plus the previously deployed inventory styling. Git push is a separate operation; verify remote tracking before reporting it complete.

## Sale modal identification previews

Product/batch and color selectors now have adjacent 72-pixel previews, updated when either selection changes. Selected variant images suppress duplicate SKU previews. With no selected variant photo, the actual batch photo may appear; missing or failed previews are hidden completely. Verified desktop/mobile selection changes, fallbacks, overflow, and existing sale/oversell behavior using isolated data. Deployed to Azure and verified live assets and protected API gates. No business records changed. Latest compact inventory styling and this feature were included in source checkpoint b56efbc. The October 26 changes described above are a separate uncommitted local update.
