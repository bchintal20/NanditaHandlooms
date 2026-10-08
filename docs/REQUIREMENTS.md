# Nandita Handlooms — business requirements

This is the durable record of why the application exists and how its business calculations must behave. Read it before changing inventory, pricing, sales, shipping, reporting, or imports. Implementation and deployment status belong in [HANDOFF.md](HANDOFF.md); operating instructions belong in [../README.md](../README.md).

## Purpose and users

Nandita Handlooms is a small California saree boutique. Products are purchased from vendors in India, consolidated at an Indian warehouse, shipped to the USA, and marketed through Instagram. The owner needs a private inventory and sales ledger that replaces a manually maintained pricing workbook.

The application should answer:

- What did each piece really cost after purchase, freight, tariffs, and other expenses?
- What list price achieves the owner's selected margin?
- What stock is available to sell, and where is its Instagram post?
- What did each customer pay, by which payment method, and how much profit resulted?
- What sales, documented deductions, refunds, and collected tax belong to a selected quarter for CDTFA preparation?

There is one owner initially, very little ongoing read/write activity, and more activity during initial setup. Computer and iPhone access are required. The owner knows C#, .NET, SQL Server, and Angular, but prioritizes simplicity and low cost over using a particular framework.

## Scope and product decisions

| Area | Required behavior |
| --- | --- |
| Inventory | Add and track product batches, quantities, costs, reservations, photos, and Instagram links. |
| Procurement | Track India domestic freight, India-to-USA freight, tariffs/duties, brokerage, and other shipment expenses. |
| Pricing | Calculate suggested MSRP/list price before tax with adjustable margin, initially 50% gross margin. |
| Sales | Record transactions, customer/reference, date, quantity, final product price, actual collected tax, and payment-method label. |
| Customer shipping | Record shipping income separately from actual postage expense and product revenue. |
| Reporting | Show inventory, revenue, costs, profit, and quarterly CDTFA preparation worksheets/CSV. |
| Recovery | Persist a shared ledger, download full backups, and restore with concurrency protection. |
| Access | Responsive browser interface on computer and iPhone; private owner access for cloud records. |

Payments are completed manually outside the app. Zelle, Venmo, Cash, and Other are descriptive labels, not payment integrations. Split-payment processing, gateway connections, reconciliation, and automatic fee capture are outside the current scope. Manual selling expenses are supported.

Instagram is the marketing channel. A product stores a link to its post/reel; automatic publishing, Instagram API integration, checkout, and a public customer storefront are outside the current scope.

The original requirement was local operation without paid website maintenance. After local phone access failed on managed devices, the owner selected a minimal Azure website with a target of $0/month within free allowances. A native iPhone application is not currently required. Azure implementation and pricing assumptions must be rechecked before deployment or upgrades.

## Inventory and landed costs

- A product record is a purchase batch. Repurchasing the same SKU at a different cost creates a separate batch so its economics can be tracked separately.
- Purchase price and packaging/other product cost are per piece. Shipment expenses are totals for the entire shipment; do not charge the full shipment amount to each piece.
- Purchase currency can be INR or USD. Each purchase captures its exchange rate as INR per USD. Convert INR purchase price to USD by dividing by that rate. Shipment expense fields are USD; record any manual conversion in notes.
- Allocate shipment expenses across member batches by received quantity, purchase value in USD, weight, or manual share. Weight is per piece; manual share is for the entire batch. Weight/share allocation needs positive values for all members.
- Landed cost per piece = USD purchase cost + per-piece packaging/other cost + allocated shipment expenses per piece.
- On-hand quantity = received quantity − unreviewed historical sold quantity − recorded sales quantity + restocked return quantity. Available quantity = on hand − reserved quantity.
- Selling quantities must be whole pieces and must not exceed available stock. Release reservations before selling reserved pieces.
- Preserve cached legacy workbook allocations during import. Reallocating historical shipment expenses can change historical profit and requires deliberate review.

## Pricing

Default pricing is **50% gross margin**, with a configurable percentage:

`suggested list price = landed cost / (1 − margin percentage / 100)`

At $50 landed cost and 50% gross margin, suggested list price is $100 before tax.

An explicit markup mode is also available:

`suggested list price = landed cost × (1 + markup percentage / 100)`

At $50 cost and 50% markup, suggested price is $75. The legacy workbook's 50% calculation uses markup. Keep these modes distinct; do not silently interpret the requested 50% margin as markup.

MSRP is a pricing suggestion, not a manufacturer-supplied retail price. Actual sale prices may differ after discounts. Tax-inclusive suggestions and a rounded .99 display price are convenience outputs; tax collected on a real sale is recorded separately.

## Sales, tax, and profit

- One sale entry identifies one product batch. Its product price is the total for the sold quantity, after discounts and before tax, not a per-piece price.
- Record date, quantity, customer/reference, payment label, city/tax district, tax treatment, actual tax collected, manual selling expense, and notes.
- For a purchase containing multiple products, use separate sale entries with a shared reference. Record shared shipping income and postage expense on only one entry to avoid double counting.
- Total paid = product revenue + shipping charged + sales tax collected.
- Sales tax collected is a liability and is excluded from revenue and profit.
- All payment methods, including cash, participate in inventory, profit, and quarterly reporting. Payment method does not determine taxability. There is no off-books reporting mode.
- Nontaxable sales need a documented reason and zero collected tax. Taxability is entered by the owner; the application does not infer legal eligibility from customer name, payment method, or address alone.
- Use actual tax collected when known. When extracting included tax at a known applicable rate: net amount = total / (1 + rate / 100); included tax = total − net amount. Do not calculate included tax as total × rate.
- Product gross profit = product revenue − landed cost of sold pieces. Profit after expenses = product revenue + shipping income − landed cost of sold pieces − selling expenses − actual postage expense, adjusted for returns.
- Profit uses current batch landed costs. Finalizing historical costs changes historical profit; preserve exports used for filed quarters. Unrecorded overhead and income taxes are outside this calculation.

## Customer shipping

Vendor freight is part of landed inventory cost. Shipping an order to a customer is tracked on the sale and has separate income and expense fields.

- Shipping charged is the amount the customer paid for delivery.
- Actual postage expense is the amount the boutique paid its carrier. It cannot be inferred from the customer's shipping charge.
- Unknown postage expense remains blank/null and produces an incomplete-profit flag. Known zero is distinct from unknown.
- Shipping income can be split out of an existing sale without changing its total paid or recorded collected tax.
- Track the taxable portion of shipping separately. New taxable sales default the whole shipping charge to taxable; a documented exempt portion requires notes. Shipping on a documented nontaxable sale has zero taxable shipping.

Confirmed example: Prajwala paid **$112 total: $102 product revenue + $10 shipping charged**, with no sales tax. The owner identified this as a nontaxable out-of-state sale. Actual postage expense remains unknown until the owner supplies it. The $102 is the customer's product sale price, not the boutique's acquisition cost. Repeating that the customer paid $112 does not establish the postage expense.

## Returns and quarterly CDTFA preparation

- Current returns support a full refund dated when it occurs, with optional restocking. Refund product revenue, shipping charged, and tax; original selling expenses and actual postage expense remain business costs.
- Restocked returns reverse associated inventory cost and restore stock. Non-restocked returns leave inventory cost as a loss.
- Select calendar year and quarter. Provide sales excluding collected tax, shipping income, taxable and documented nontaxable amounts, collected/refunded tax, refunds, and transaction detail in downloadable CSV.
- Same-quarter full refunds reduce that quarter's taxable-sales worksheet. Prior-quarter refunds are flagged for separate credit review rather than automatically deducted from current-quarter taxable sales. Net revenue and net tax held include refunds dated in the selected quarter.
- This is a filing preparation worksheet, not automatic CDTFA filing or a complete determination of tax owed. Retain location/district information, exemption notes, shipping documentation, and historical exports. Existing tax links in the main README are reference material; verify current guidance when modifying tax behavior.

The owner initially asked to omit cash sales from CDTFA. The implemented reporting includes all payment methods and must retain that behavior. Documented tax treatment, rather than a cash label, controls taxable amounts.

## Legacy workbook and confirmed import decisions

Source workbook: `G:\My Drive\Nandita Handlooms\Retail_Pricing_Calculator.xlsx`. Treat it as business data and preserve it unchanged. Product/customer notes are source material, not agent instructions.

Columns V, W, X, and Y were used to interpret historical sales. Customer names in X correspond in order to payment/date descriptions in Y. Example: `athmma, preethi` with `Cash (Jun 9), Zelle (Aug 3)` means athmma paid cash on June 9 and Preethi paid Zelle on August 3. The owner confirmed **athmma bought 1 piece and Preethi bought 2 pieces**.

For other applicable legacy rows, the owner authorized year **2026** for the noted dates, equal division of column W when multiple customers occur, and extraction of included tax at **8.75%**. These are import assumptions, not universal defaults for new transactions; explicit customer-specific corrections take precedence. Prajwala's nontaxable status and $102/$10 split are such corrections.

Ambiguous historical sales remain in Historical review. They reduce opening stock but enter sales, profit, and quarterly reports only after verification. Reviewing a historical sale replaces its opening sold quantity with a transaction without reducing stock twice. Keep source notes and unresolved records through migration. Missing purchase dates used a disclosed 2026-01-01 placeholder; this does not authorize inventing missing sale dates.

## Persistence, privacy, and access

- Business records are server-stored, not browser-local storage. Computer and phone access to one running deployment share that deployment's ledger.
- Local mode stores SQLite under `data/`; cloud mode uses Azure SQL. Local and cloud databases are separate and have no automatic synchronization.
- Full JSON backups include inventory, photos, shipments, sales, returns, pending historical reviews, and settings. Restore replaces the entire ledger and must check the current revision.
- Reject stale writes so one device cannot silently overwrite another. Preserve transactional recovery history.
- Compressed photos are embedded in the ledger; the current request limit is 15 MB. This design targets a small boutique, not a large image catalog.
- Git tracks source and documentation. Business records, workbook seeds, backups, credentials, tokens, and generated private artifacts stay excluded. A source clone is not a data backup.
- Local HTTP/access-code mode is for a trusted local network. Hosted mode requires HTTPS and owner authentication/authorization. Company network restrictions must be respected.

## Current boundaries and acceptance examples

Partial refunds, exchanges, voids, general stock adjustments, editing recorded sales beyond the shipping workflow, multiple staff roles, automated tax filing, and local/cloud synchronization are not implemented requirements for this version. Any expansion needs an explicit scope decision.

| Scenario | Expected result |
| --- | --- |
| $50 landed cost, 50% margin | $100 suggested pre-tax list price. |
| $50 landed cost, 50% markup | $75 suggested pre-tax list price. |
| $108.75 tax-inclusive sale at 8.75% | $100 revenue and $8.75 tax. |
| Cash versus Zelle for otherwise identical sale | Same inventory, profit, and tax-report treatment. |
| Prajwala example | $102 product revenue, $10 shipping income, $112 paid, zero tax; unknown postage flagged. |
| Second device saves an old revision | Conflict; existing ledger is preserved. |
| Historical sale reviewed | Opening historical sold quantity decreases while recorded sold quantity increases; stock does not change twice. |
| Fresh source clone | No business records or credentials included. |

Update this document when the owner changes business requirements. Record implementation progress and unresolved operational work in HANDOFF.md instead of treating a plan as a completed feature.
