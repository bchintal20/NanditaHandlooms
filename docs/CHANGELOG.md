# Implemented milestones and evidence

This is historical context. Current state is in [Handoff](HANDOFF.md); routine procedures are in [Operations](OPERATIONS.md). These milestones summarize earlier accumulated session notes, rather than instructing a new chat to repeat setup.

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

Organized requirements, screen guide, architecture, operations, current handoff, and history as Markdown in the repository. Root README indexes them; AGENTS directs future chats by task. Source documentation is separate from private release evidence and business backups. No application data or deployed code is changed by this documentation update.
