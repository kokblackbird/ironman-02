# Automotive Repair Pricing Helper (Console, .NET 8)

A console app that builds quotes/invoices for automotive repair by combining materials, shipping, labor, research/admin, and shop fees. It supports a parts catalog, CSV import/export, and generates a modern HTML invoice/quote.

## Features
- Material entry with per-line controls:
  - Cost, shipping, customer-provided flag
  - Large/bulky handling/storage fee
  - Waste/recycling fee
  - Apply margin/markup
  - Taxable per item
- Parts catalog with unique part numbers (pre-enter once, reuse later)
- CSV import (template provided) and per-run CSV export of the job
- Pricing modes: margin or markup
- Minimum invoice and minimum effective hourly checks
- Exports:
  - HTML invoice/quote to `C:\temp\`
  - Text summary to the app folder

## Requirements
- .NET 8 SDK
- Windows for default output paths (uses `C:\temp\...`)

## Quick Start
1) On startup (optional): add parts to the catalog  
   - Each entry includes: part number (unique), description (shown to the customer), cost, shipping, taxable, large-item status & fee, waste handling & fee, and whether margin applies.  
   - Saved to `C:\temp\automechanic\parts_catalog.csv`.

2) Add materials for the current job  
   - Import from CSV (template offered), then optionally add items manually.  
   - Or add all materials manually.  
   - You can also pull a single item from the parts catalog by entering its part number.

3) Enter labor, tax, and fees  
   - Labor hours and rate, research/admin hours and rate.  
   - Vehicle storage/handling fee (flat).  
   - Choose Margin or Markup and set targets.  
   - Optionally set a pre-tax minimum invoice and a minimum acceptable effective hourly.

4) Review and export  
   - The console prints a breakdown, recommended price, and final price (after minimums).  
   - Saves TXT to the app folder and HTML to `C:\temp\`.  
   - Saves job CSVs to `C:\temp\automechanic\orders\` for later reuse.

## Files & Folders
- Parts catalog (created if missing)  
  - Path: `C:\temp\automechanic\parts_catalog.csv`  
  - Columns:  
    `partNumber,description,cost,shipping,taxable,isLarge,largeFee,wasteHandled,wasteFee,applyMargin`  
  - Notes:
    - Description is customer-facing.
    - Part numbers must be unique.

- Materials template (created if missing)  
  - Path: `C:\temp\automechanic\materials_template.csv`  
  - Columns:  
    `name,cost,shipping,customerProvided,isLarge,largeFee,wasteHandled,wasteFee,applyMargin,taxable`  
  - Booleans accept: `y/yes/true/1` or `n/no/false/0`.

- Job saves (created per run)  
  - Folder: `C:\temp\automechanic\orders\`  
  - Files:
    - `order_<timestamp>_materials.csv` (all line items)
    - `order_<timestamp>_settings.csv` (labor/tax/fees/pricing settings)

- Exports  
  - TXT: application folder  
  - HTML: `C:\temp\` as `QUOTE_<timestamp>.html` or `INVOICE_<timestamp>.html`

## Pricing Model
- Margin (target M): `Price = Cost / (1 - M)`  
- Markup: `Price = Cost * (1 + markup)`  
- Margin-eligible base: materials (if `ApplyMargin=true` and not customer-provided) + labor + research.  
- Pass-through costs: materials marked no-margin or customer-provided are billed at cost.

## Taxes & Fees
- Per-item taxable flag controls whether a material’s amount (cost+shipping and its share of the margined total) is taxed.
- Labor, research, and all fees (per-item large & waste, and vehicle storage/handling) are taxed only if you choose to “Apply tax to labor/fees.”
- Fees are added as revenue (no additional margin).

## Minimums and Worth-It Check
- A pre-tax minimum invoice can raise the final price above the recommendation.  
- Worth-it check shows max hours before you drop below your minimum effective hourly given non-labor costs.

## CSV Import Behavior
- Importer maps by column names; extra columns are ignored and missing columns fall back to defaults.
- If both `name` and `description` exist, `name` is used for the customer-facing label.
- Decimals accept Invariant (`.`) and `en-US` formats.

## Running
From the project directory: