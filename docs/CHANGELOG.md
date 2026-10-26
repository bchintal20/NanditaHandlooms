# Implemented milestones and evidence

This is historical context. Current state is in [Handoff](HANDOFF.md); routine procedures are in [Operations](OPERATIONS.md). These milestones summarize earlier accumulated session notes, rather than instructing a new chat to repeat setup.

## October 26, 2026 — sale maintenance and inventory cleanup

Added in-place sales correction with original-value preservation, stock recalculation, and refunded-sale structural restrictions. New sales default product revenue and tax from the rounded .99 customer label price; manual overrides and the tax-extraction button remain available. Added confirmed deletion for unused/test inventory, protected against sales/history/reservation references, with a shared-shipment cost warning. Python and C# now validate these state transitions; explicit backup restore retains its replacement semantics. No schema migration or business-data import was performed.

Passed synthetic desktop/mobile sale-maintenance tests, existing variant/media/oversell tests, 8 Python tests, and 55 C# checks. Updated Requirements, Features, Architecture, Operations, and Handoff. The owner confirmed the local result and subsequently authorized commit, push, and Azure deployment. Release verification is pending.

## Local ledger and workbook migration

Built purchase-batch inventory, procurement/shipment allocations, adjustable margin/markup MSRP, manual sales/payment labels, tax-exclusive revenue/profit, full refunds, quarterly CSV, historical review, access code, and full JSON recovery. Initial workbook import preserved 22 batches, 58 received pieces, 15 opening sold pieces, and 43 on hand, including cached landed-cost allocations. These are **initial import figures**, not current inventory.

Owner authorized interpretation of historical customer/payment columns and 2026/8.75%/equal-split assumptions for applicable rows. Customer-specific corrections take precedence. Added shipping income versus actual postage and the confirmed $102 + $10 Prajwala receipt. Unknown postage remains flagged.

## Private Azure deployment

Selected SWA Free with integrated .NET 10 Functions and SQL serverless free offer after managed devices could not reach the local LAN app reliably. Provisioned the personal-account resources listed in Operations, imported the ledger with equivalence checks, and retained historical-review data. Code-only updates preserve the database.

Owner invitation initially failed/expired and work-account sessions were denied. The personal account initially had only anonymous/authenticated roles. After fresh invitation redemption the owner confirmed inventory loaded. Azure CLI authorization and website owner authorization are separate workflows.

## Color variants

Added multiple named colors under one batch/SKU with assigned quantities, photos, Instagram links, color sale selection and refund/restock tracking. Parent costs remain shared; no stock duplication. Validators reject over-allocation, color overselling, and deletion of referenced colors. Isolated desktop/mobile and calculation/server tests covered the flows; owner approved and code was deployed.

## Inventory media and price labels

Added uploaded-image previews/enlargement and best-effort public Instagram embeds. Bold tax-inclusive .99 suggestions share the calculation used by labels. Added selectable products/colors/copies, custom dimensions, 300-DPI PNG download/share, and one-label-per-page browser printing/PDF for the owner's SUPVAN T50M Pro workflow. Software tests checked desktop/mobile previews, price boundaries, PNG dimensions, PDF pages, and staged CSP. Physical printing/roll size and live Instagram availability remain unverified.

## Startup diagnostics and image-heavy loading fix

Added explicit loading/error/manual-refresh states and bounded transient retries. Owner phone Chrome still hung after a large batch of attached photos. Read-only SQL inspection found a full payload of approximately 10.3 million characters; photos made the all-ledger page response a concrete bottleneck. Public health or republishing alone did not prove authenticated owner loading. Other transient worker/SQL behavior was observed; its exact cause was not conclusively established.

Implemented opt-in lazy metadata projection inside SQL, private photo GETs, image-reference hydration on saves, full gzip backups, metadata-backup rejection, and script fingerprinting. Original photos/records remain stored in the same JSON ledger, with no schema migration or added paid service. A measured metadata response was approximately 31.5 thousand characters, versus the full image-heavy ledger.

Evidence: 40 C# checks including preserve/edit/remove/replace photo cases and forged/stale references; read-only live projection/full-ledger roundtrip equivalence; desktop/mobile mocked UI verifying no Overview photo fetch, Inventory lazy fetch, actual JPEGs in backups, and preserved images on metadata saves. Related startup/financial/variant/label regressions passed. Live assets matched stage, health returned 200, anonymous state/full-backup/photo requests were blocked. No live sales or other business records were written for diagnosis/testing. Temporary operator-IP SQL access was removed.

**Owner subsequently confirmed: “It loaded now” on phone Chrome.** Earlier pending phone-confirmation notes are superseded. A live business write was not performed solely as release verification.

## Documentation consolidation

Organized requirements, screen guide, architecture, operations, current handoff, and history as Markdown in the repository. Root README indexes them; AGENTS directs future chats by task. Source documentation is separate from private release evidence and business backups. That documentation-only consolidation did not change application data or deployed code.

## October 12, 2026: independent boutique accounts

Added invite-only email/password accounts, 30-day remember-me, 12-hour normal sessions, saved recovery codes, password change/reset, owner invitation/reset/disable/reinvite controls, and full-access members. No external account-provider switching or email service is required. Salted PBKDF2 password hashes and hashed random tokens are stored separately from ledger JSON. Serializable rate limits/token consumption, secure HttpOnly cookies, exact-origin writes, and immediate session revocation protect access. Microsoft principals no longer authorize data endpoints.

Passed 48 synthetic/isolated-SQL account checks and 50 ledger/API checks, including concurrent token use, persisted failed attempts, origin denial, and platform-header bypass denial. Desktop/mobile account workflows, financial/local, startup, lazy-photo/full-backup, cloud-navigation, variant, and label regressions passed. Fixed the historical-review function's collision with native browser History so invitation fragments are removed correctly.

Additive account table/grants migration verified the financial revision/payload unchanged. Initial SQL cold-start attempt timed out; retry succeeded and temporary exact-IP operator rules were removed. Deployed via existing code-only workflow; all live assets matched stage, anonymous ledger/backup/photo/account requests and a forged session returned 401, cross-origin login returned 403, health returned 200. Opened the owner's private setup link in Chrome. The owner subsequently confirmed successful login with the new password. Password activation/sign-in are confirmed; authenticated inventory display and recovery-code storage were not separately confirmed. No password was chosen or collected by the agent.

## Inventory tile consistency

Constrained inventory columns with minmax(0,1fr), standardized preview height, and used equal tile rows with bottom-aligned controls. Long SKU/vendor/title text is bounded and variant strips scroll inside the tile. Synthetic browser checks reproduced the prior width overflow and passed at 1440/1000/390 pixels after the fix; existing label and variant flows passed. The owner approved the local appearance, then authorized Azure deployment. The same shared layout was deployed, with live assets verified against staging and data-access protections checked. Business records were not imported or changed.

## Default-setting feedback and visible logout

Added inline saving, timestamped success, unsaved-edit, and failure feedback next to Save defaults. Async account-control errors no longer overwrite settings feedback. Added a header Log out control on all signed-in pages; hosted mode uses boutique-session revocation, while local mode has a new same-origin logout endpoint. Both clear displayed ledger state. Verified desktop/mobile save success/failure and delayed account metadata, header logout, and isolated local session revocation. Deployed to Azure with staged/live asset equivalence and protected-data checks; the local server was restarted for its new endpoint. Included the previously approved inventory styling in the source checkpoint.

## Compact inventory tiles without variants

Removed equal-height grid rows and automatic action-button pushdown, which created unused space on cards without variants. Tile widths/previews remain consistent; height now follows content and actions have an 18-pixel gap. Layout regression checks verify compact action spacing, shorter variant-free cards, and no horizontal overflow at desktop/tablet/phone widths.

The compact-card correction is deployed to Azure; live UI assets matched staging and protected API checks passed. No business records were changed.

## Sale selection image previews

Added adjacent 72-pixel product/color thumbnails in Record a sale. Product changes rebuild available color choices and both images; color changes update the color preview. Generic photo fallback is labeled and missing/failed images are explicit. Reuses existing private cloud photo URLs; no sale, pricing, or stock logic changed.

## Sale preview cleanup

Selected variant photos now suppress the SKU/batch thumbnail. If a variant has no photo, only an actual batch photo is used; no other color is substituted. Missing/failed previews are hidden, including their empty layout column. No photo placeholders or duplicated batch fallback beside the color selector remain.

Sale-preview cleanup is deployed to Azure; desktop/mobile checks and live asset/access verification passed.
