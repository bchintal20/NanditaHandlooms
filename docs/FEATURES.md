# Feature and usage guide

For exact calculations and owner decisions, use [Requirements](REQUIREMENTS.md). For startup, recovery, and deployment, use [Operations](OPERATIONS.md).

## Overview

Shows inventory quantities/value, sales revenue excluding collected tax, product gross profit, net tax held, recent sales, and pricing/shipment guidance. Shipping income and actual postage affect profit after expenses. Missing postage and unresolved historical sales are surfaced for review. The overview does not download inventory photos in hosted mode.

## Inventory and purchasing

Add a purchase batch with SKU, description/material, vendor, date, received and reserved quantity, per-piece purchase amount, currency/exchange rate, packaging/other cost, shipment, notes, photo, and Instagram link. A repurchase at a different cost is another batch. Search inventory, inspect available stock, edit costs, record a sale, manage variants, and select price labels.

The card shows landed cost, suggested pre-tax MSRP, tax-inclusive suggestion, and a bold **Round up to .99: $… incl. tax** price. Changing default tax or pricing assumptions changes suggested labels; actual sale prices and taxes are separately entered.

Use **Delete** on an unused/test inventory card and confirm removal. Its colors and photos are removed too. Sales, historical-review references, opening sold quantities, or reservations block deletion; release reservations and save first. The confirmation warns that deleting a batch linked to a shared shipment can reallocate remaining costs/prices/profit. This does not delete sales or write off sold stock.

### Colors within one SKU

Choose **Colors / variants**. For ten different unsold colors in a batch of ten pieces, add ten named colors with assigned quantity one each. Each color can have its own photo and Instagram link. All share parent costs and MSRP.

The editor shows stock still unassigned. Assigned quantities include sales already tied to that color; historical/unassigned sales stay unassigned. Reservations apply to the whole batch. Referenced colors cannot be deleted. Select a color when selling; for several colors use separate sale entries sharing a customer/reference and charge common shipping only once. Restocked refunds restore the original color.

### Photos and Instagram

An uploaded batch photo is the primary inventory preview; otherwise a color photo can supply it. Color-specific photos/links take priority for color previews. Click photos to enlarge. If no uploaded image is available, a supported public Instagram post/reel link can show an embedded preview. A profile link is not an image source. Open-link fallback remains available.

Images are compressed JPEGs stored with the ledger. Instagram media is embedded, not scraped, imported, or published by the app. Restricted/deleted posts and blocked embeds can remain blank; upload a photo for reliable labels and inventory previews.

### Price labels

Use **Price labels** on inventory or a product. Choose products/colors from the current inventory selection, one copy each or counts based on on-hand pieces, and clear/adjust copies as needed. On-hand counts include reserved stock; printing does not change inventory.

Labels contain boutique name, product/color, SKU, large .99 tax-inclusive price, and “Tax included.” Costs, profit, and customer details are excluded.

Printer selected by owner: **SUPVAN T50M Pro**, Amazon ASIN B0CKYMWV5X. Default preview is 40 × 30 mm, with 50 × 30, 50 × 80, and custom dimensions available. Installed roll size is unconfirmed; set matching dimensions before printing. Custom width is 20–100 mm and height 15–150 mm; at most 500 labels per print operation.

- Download a 300-DPI black-on-white PNG, or share it where the browser supports file sharing. Import the image into the SUPVAN app, choose copies, and print.
- Browser Print / Save as PDF provides one label per custom-sized page. Use 100% scale, no margins, and no headers/footers. Physical output depends on printer software and roll dimensions.

The website does not directly control Bluetooth printing. Label selections/dimensions are temporary UI choices, not inventory edits.

## Shipments and landed cost

Record whole-shipment totals in USD: vendor-to-India-warehouse freight, India-to-USA freight, tariffs/duties, brokerage, and other expense. Mark estimated versus actual expenses. Link batches and allocate by quantity, purchase value, weight, or manual shares. Purchase and packaging values are per piece; shipment totals are for the entire shipment.

Finalizing expenses recalculates current landed costs and historical profit. Preserve exports supporting previously filed quarters before revising old costs.

## Sales and customer shipping

Record a date, one batch and optional color, whole quantity, customer/reference, final product revenue for all pieces before tax, actual collected tax, tax treatment/location, payment label, manual selling expense, shipping charge, postage expense, and notes. Payment labels are Zelle, Venmo, Cash, and Other; collect the payment outside the app. Filter transaction history by payment method.

The **Shipping** action on a recorded sale can split existing receipts into product revenue and shipping income and record/update postage, while retaining original total paid and tax. Use **Edit** on the Sales page to correct a recorded sale.

Enter taxable shipping separately and retain documentation for any exempt portion. Blank postage means unknown; zero means known zero. Prajwala's confirmed receipt is $102 product revenue + $10 shipping = $112, nontaxable; actual postage was not supplied.

### Sale defaults and editing

Inventory → **Sell** automatically splits the bold .99 tax-inclusive label price into **Product total** before tax and **Sales tax**, multiplied by quantity. At $108.99 per piece and 8.75% tax, defaults are $100.22 product total and $8.77 tax. Shipping income is separate; taxable shipping adds its own tax. Quantity and tax-treatment changes update these suggestions until you manually edit product total or tax. **Use suggested price** reapplies the label calculation; **Extract tax from total paid** remains available. Confirm the actual receipt amounts before saving.

Choose **Edit** beside a recorded sale. The form loads the original values, including discounts, collected tax, shipping, payment label, and notes. Saving replaces that transaction, keeping its ID. Its own previously sold quantity is available for correction, even if the batch/color is otherwise sold out. Increasing quantity or changing batch/color still requires sufficient available stock. Editing does not automatically reprice the sale; use the suggestion button only when you intend to change its amounts.

For a fully refunded sale, batch/color/quantity are locked; details and amounts remain correctable. A corrected amount also changes the derived refund and its reports. The sale date must be on or before its refund date. Corrections can change earlier-quarter reports, so retain exports supporting filed returns. Partial refunds, sale deletion, and voids remain unsupported.

## Full refunds

Record the refund date and whether pieces were restocked. Full refunds reverse product/shipping revenue and tax. Original selling/postage expenses remain costs. Restocking reverses associated cost of goods and restores inventory/color stock; a non-restocked return leaves inventory cost as a loss. Partial refunds and exchanges are not implemented.

## Historical review

Imported workbook sold quantities reduce stock before verification. Review each unresolved row, split customers where needed, and verify quantity, date, payment, receipt amount, and tax. Recording the verified sale replaces the opening sold quantity so inventory is not reduced twice. Unverified rows remain outside transaction/profit/quarterly reports.

Owner-confirmed import assumptions and exceptions are in Requirements. Original workbook notes remain source evidence; the workbook is unchanged.

## Quarterly report

Choose calendar year and quarter. View receipts excluding collected tax, taxable/documented nontaxable amounts, shipping treatment, collected/refunded tax, refunds, and transaction detail. Download CSV, including color information; exported cells are escaped against spreadsheet formula injection.

Same-quarter refunds reduce the taxable-sales worksheet. Earlier-quarter refunds are flagged for separate credit review. All payment methods are included. This prepares records for review and filing; it does not automatically file or determine every CDTFA return field.

## Settings, backup, and access

Set boutique name, margin/markup method and percentage, default tax rate, and INR/USD exchange rate. New purchases retain their captured exchange rate. Download complete JSON backups with products, variants/photos, shipments, sales, returns, pending reviews, and settings. Restore replaces the whole ledger with revision checks.

Business data lives on the server, not browser storage. Different devices using the same website share the ledger. Local and hosted modes have separate ledgers. Hosted mode has boutique email/password sign-in/sign-out and remembered sessions; local mode uses a console access code.

Hosted loading shows progress, retries transient failures, and exposes a useful error/manual refresh action. Authorization errors are not retried as database wake-up failures. Photos load when inventory is viewed rather than blocking initial overview.

## Boutique accounts

Independent email/password login, owner invitations, member access, remember-me, recovery codes, and account management are described in [ACCOUNTS.md](ACCOUNTS.md). This is deployed, and the owner confirmed successful password sign-in; check HANDOFF for verification details. Settings & backup contains the account controls. Microsoft/GitHub account selection is no longer needed to use the hosted ledger.

Inventory tiles use equal responsive column widths, content-sized heights, a consistent preview area for photos/Instagram/placeholders, and bounded color strips. Actions follow the content with a small fixed gap; cards without variants do not reserve empty variant space. Long SKU/vendor/title text stays inside each card; title attributes preserve the full text on hover.

Saving defaults displays Saving…, then a timestamped success message beside the save button. Editing defaults shows Unsaved changes; failed writes show Not saved and retain the entered values. Account-control errors are displayed separately. Log out is available in the header on every signed-in screen; the existing settings sign-out remains available. Logout clears the screen and revokes the current session in both hosted and local modes.

The Record a sale modal shows the selected variant photo beside Color / variant and suppresses the SKU/batch preview when that variant has a photo. Otherwise the actual batch photo can appear beside Product / batch. It does not borrow a different variant’s photo. Missing or failed images are hidden entirely, and their selectors use the freed width. No “No photo” placeholders are displayed.
