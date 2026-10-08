# Nandita Handlooms

For the business purpose, agreed rules, and scope, read [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md). For a future chat or developer taking over, start with [docs/HANDOFF.md](docs/HANDOFF.md). [AGENTS.md](AGENTS.md) points coding agents to these documents.

The original local app is described below. The Azure version, validation commands, and migration procedure are documented in [cloud/README.md](cloud/README.md). Azure resources and code are deployed; owner access verification is in progress. The website is [Nandita Handlooms](https://gray-grass-0cab2cd10.6.azurestaticapps.net/).

Git tracks source and infrastructure only. The SQLite database, private workbook import, logs, photos/previews, local settings, generated migration exports, and Azure sign-in cache are excluded. A fresh clone starts without your business records; keep separate data backups.

A local inventory, landed cost, pricing, and sales ledger. No packages, paid hosting, or accounts are required. Python 3.10+ is required and is already installed on the computer used to build this app.

## Open it

Double-click **Start Boutique.bat**. A browser opens. Enter the six-digit access code printed in the console window. Keep that window open while using the app. Closing it stops the app; records remain saved.

For your iPhone, use **Stop Boutique.bat** to stop any existing instance, then double-click **Start Boutique WiFi.bat**. Connect both devices to the same trusted private Wi-Fi. In Safari, enter the `http://192.168…:8765` (or other local address) printed in the window. Enter the same access code. If Windows Firewall asks, allow Python only on **Private networks**. Do not forward a router port or allow Public networks. Some guest networks block devices from connecting to each other.

Safari's Share → Add to Home Screen can provide a shortcut. The computer must remain on and awake. The shortcut does not provide independent offline access. No app is published to the App Store. The Wi-Fi option runs a tiny server **on your computer**, not a hosted service. Computer-only use works without an internet connection. Wi-Fi use needs a local network, not an internet subscription.

The initial access code is saved in `data/access-code.txt`. You may change it while the app is stopped. This is light protection on a trusted LAN, not internet-grade encrypted access; local HTTP traffic is not encrypted. Don't expose the app to the internet.

## First use

1. Review your boutique name, pricing method, current exchange rate, and correct local sales tax rate under Settings & backup. Your imported workbook supplies 8.75% tax and 90 INR/USD; verify these before recording new transactions. New purchases capture their own exchange rate.
2. Add a shipment. Enter shipment totals in USD for India domestic shipping, India-to-USA shipping, tariffs/duties, brokerage, and other shipment expenses. Convert INR invoice amounts yourself and record the conversion in notes.
3. Add products and assign the shipment. Purchase amounts are **per piece**; shipping amounts are **whole shipment totals**. Add another batch when repurchasing the same SKU at a different cost. Add a photo and Instagram link if useful.
4. Choose quantity, purchase value in USD, weight, or manual shares to allocate shipment costs. Weight is **per piece**. Manual shares represent **the entire batch**. All members need positive weights or shares for those allocation methods.
5. Record a sale with its final price after discounts and before tax, plus the actual tax collected. Enter Zelle, Venmo, Cash, or Other; payments happen outside the app. One sale entry is one product batch; use a shared reference for multiple products in one customer purchase.
6. Review the quarterly report and download its CSV. Export a full JSON backup regularly.

## Calculations and limitations

- Landed cost per piece = purchase converted to USD + allocated shipping/duty/brokerage + per-piece packaging/other cost.
- Suggested list price = landed cost / (1 − target gross margin), or landed cost × (1 + markup), depending on the selected mode. 50% gross margin means a $50 cost gives a $100 suggested price; 50% markup gives $75. The app starts at 50% gross margin as requested. Your workbook's 50% column uses markup; select Markup in Settings to match it. Prices exclude sales tax. A tax-inclusive suggestion and rounded .99 display price also appear in inventory. Actual sales prices may differ.
- Available inventory = received − sales + restocked returns − reserved. Release reserved pieces before selling them.
- Profit uses revenue excluding tax and current landed costs. Finalizing estimated shipment costs updates historical profit. Keep the exported worksheet used for a filed quarter. Selling expenses are entered manually; unrecorded overhead and income taxes are not included.
- Full refunds are recorded on their refund date, with optional restocking. A non-restocked return reverses revenue but leaves its inventory cost as a loss. Original selling expenses remain. Partial refunds, exchanges, voids, damaged-stock adjustments outside returns, and editing a recorded sale are not in this first version. Check entries before saving; use backups to recover from mistakes.
- Every payment method is included in reports. Nontaxable sales require a reason; the app does not decide whether a sale qualifies.
- Sales tax collected is not the same as sales tax owed. Quarterly reports prepare data, not a complete CDTFA return. You must review location/district allocation, correct rates, use tax, credits, and any other required return fields.
- Same-quarter full refunds reduce the worksheet's taxable sales. Prior-quarter refunds are flagged for separate CDTFA credit review, not automatically deducted from this quarter's taxable sales. Net revenue and net tax held include all refunds dated in the selected quarter.
- Import replaces the entire ledger. Each save checks the revision to prevent one device silently overwriting another. Refresh before editing on a second device. Long-open forms can retain old values: refresh and reopen them after another device makes changes.
- Compressed JPEG photos are stored in the ledger. Keep backups under 15 MB; the app rejects larger requests. This first version is intended for a small boutique.

## Data and recovery

The database is `data/boutique.sqlite3` next to the app. The previous state is preserved in `data/last-good-backup.json` before every write. Full JSON backups include products, photos, shipments, sales, returns, and settings. To recover, use Settings & backup → Restore. Copy the entire app folder, including `data`, to an external backup while the app is stopped. Browser storage is not used for business data.

## Existing workbook import

The initial import preserves 22 product batches, 58 received pieces, 15 sold pieces, and 43 on hand. The workbook's cached per-piece costs are preserved and reconcile to its $3,903.268888888889 total landed cost. Workbook purchase dates were absent: imported product notes disclose a 2026-01-01 placeholder; no sale dates are invented.

The 15 sold pieces and $1,871.87 recorded receipts are in Historical review. They reduce opening inventory but remain outside transaction, profit, and quarterly reports until reviewed. Split each source row into individual sales with actual dates, amounts, tax collected, and payment methods. Original notes remain preserved. Reviewing a sale replaces its opening sold quantity with a verified transaction without changing stock. For corrected actual receipt amounts, the remaining unreviewed balance is only an estimate and should be checked against records.

The source workbook is not edited. Its quarterly figures are not imported: its tax-inclusive extraction formula subtracts `total × tax rate`, whereas the app extracts net sales using `total / (1 + tax rate)`. Actual collected tax still takes precedence. The imported shipping records retain cached allocations rather than redistributing historical invoice totals. Validate them against invoices before updating old costs.

## Customer shipping

Each sale can record product revenue, shipping charged to the customer, and actual postage/shipping expenses separately. Enter shared order shipping on only one entry when an order contains multiple products. Leave an unknown postage expense blank; the app flags incomplete profit instead of guessing it. Use the Shipping button on an existing sale to split its original revenue into product and shipping income, or add/update postage expenses. The original total paid and sales tax collected remain unchanged.

Profit after expenses includes shipping income and subtracts actual shipping expense. Product gross profit excludes shipping income and postage expense. Full refunds return the product amount, shipping charged, and tax; the actual postage expense stays as a business cost.

The taxable portion of shipping is entered separately. New taxable sales default the whole charge to taxable; a documented exempt portion can be entered explicitly with shipping notes. Shipping on an approved nontaxable sale is recorded as nontaxable. The quarterly worksheet includes shipping in total receipts and lists documented exempt shipping on taxable sales separately. See [CDTFA shipping guidance](https://www.cdtfa.ca.gov/formspubs/pub100/). Retain carrier receipts and shipping documentation; the app does not determine legal tax exemptions.

## Sources checked during development

- [CDTFA online filing instructions](https://cdtfa.ca.gov/cros/online-filing-instructions.htm)
- [CDTFA return instructions](https://cdtfa.ca.gov/formspubs/cdtfa401inst.pdf)
- [CDTFA local tax rates](https://www.cdtfa.ca.gov/taxes-and-fees/sales-use-tax-rates.htm)
- [Apple: turn a website into an app in Safari](https://support.apple.com/guide/iphone/open-as-web-app-iphea86e5236/ios)

## Checks

Run `python test_server.py` and `node test_calculations.js` in the app folder. No dependencies are needed. Tests use a temporary database and do not modify your ledger.
