using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;

class Program
{
 private sealed class MaterialLine
 {
 public string Name { get; set; } = string.Empty;
 public decimal Cost { get; set; }
 public decimal Ship { get; set; }
 public bool CustomerProvided { get; set; }
 public bool IsLarge { get; set; }
 public decimal LargeFee { get; set; }
 public bool WasteHandledByShop { get; set; }
 public decimal WasteFee { get; set; }
 public bool ApplyMargin { get; set; } = true; // whether this material (and its shipping) participates in margin/markup
 public bool Taxable { get; set; } = true; // whether this item (cost + shipping + its margined share) is taxable
 public string? PartNumber { get; set; } // optional when loaded from catalog
 }

 static void Main()
 {
 Console.OutputEncoding = System.Text.Encoding.UTF8;
 var ci = CultureInfo.GetCultureInfo("en-US");

 Console.WriteLine("=== Automotive Repair Pricing Helper ===");
 Console.WriteLine("Enter your materials, labor, tax, and margin details.\n");

 // CSV import directory and templates
 string baseCsvDir = @"C:\\temp\\automechanic";
 Directory.CreateDirectory(baseCsvDir);
 string templatePath = Path.Combine(baseCsvDir, "materials_template.csv");
 EnsureCsvTemplate(templatePath);
 string catalogPath = Path.Combine(baseCsvDir, "parts_catalog.csv");
 EnsurePartsCatalog(catalogPath);

 // Optional: manage parts catalog first
 bool manageCatalog = ReadBool("Enter new part numbers into catalog now? (y/n)", false);
 if (manageCatalog)
 {
 var existingParts = LoadCatalogPartNumbers(catalogPath);
 while (true)
 {
 string partNum;
 while (true)
 {
 partNum = ReadString(" Part number (unique, leave blank to stop)", "");
 if (string.IsNullOrWhiteSpace(partNum)) break;
 if (!existingParts.Contains(partNum)) break;
 Console.WriteLine(" Part number already exists. Please enter a unique value.");
 }
 if (string.IsNullOrWhiteSpace(partNum)) break;

 string description = ReadString(" Description (shown to customer)", "");
 if (string.IsNullOrWhiteSpace(description)) description = partNum;

 decimal cost = ReadDecimal(" Cost ($)",0m);
 decimal ship = ReadDecimal(" Shipping ($)",0m);
 bool taxable = ReadBool(" Taxable? (y/n)", true);
 bool isLarge = ReadBool(" Large/bulky item? (y/n)", false);
 decimal largeFee = isLarge ? ReadDecimal(" Large item handling/storage fee ($)",0m) :0m;
 bool wasteHandled = ReadBool(" Handle waste/recycling? (y/n)", false);
 decimal wasteFee = wasteHandled ? ReadDecimal(" Waste/Recycling fee ($)",0m) :0m;
 bool applyMargin = ReadBool(" Apply margin/markup to this item? (y/n)", true);

 AppendCatalogEntry(catalogPath, partNum, description, cost, ship, taxable, isLarge, largeFee, wasteHandled, wasteFee, applyMargin);
 existingParts.Add(partNum);

 bool another = ReadBool(" Add another part number? (y/n)", true);
 if (!another) break;
 Console.WriteLine();
 }
 Console.WriteLine($"Catalog saved: {catalogPath}\n");
 }

 // MATERIAL ENTRY OR IMPORT
 List<MaterialLine> materials = new();
 bool importFromCsv = ReadBool("Import materials from CSV? (y/n)", true);
 bool addMoreAfterImport = false;
 if (importFromCsv)
 {
 // List CSVs and choose one
 var csvFiles = new List<string>(Directory.GetFiles(baseCsvDir, "*.csv"));
 if (csvFiles.Count ==0)
 {
 Console.WriteLine("No CSV files found. Using template and continuing.");
 csvFiles.Add(templatePath);
 }
 Console.WriteLine("Available CSV files in " + baseCsvDir + ":");
 int defaultIndex = Math.Max(0, csvFiles.IndexOf(templatePath));
 for (int i =0; i < csvFiles.Count; i++)
 {
 Console.WriteLine($" [{i}] {Path.GetFileName(csvFiles[i])}{(i == defaultIndex ? " (default)" : string.Empty)}");
 }
 int chosenIndex = ReadInt("Select CSV index", defaultIndex,0, csvFiles.Count -1);
 string chosenPath = csvFiles[chosenIndex];

 try
 {
 materials = LoadMaterialsFromCsv(chosenPath);
 Console.WriteLine($"Imported {materials.Count} material line(s) from {chosenPath}.");
 }
 catch (Exception ex)
 {
 Console.WriteLine($"Failed to import CSV: {ex.Message}. Continuing with manual entry.");
 materials.Clear();
 }

 addMoreAfterImport = ReadBool("Add more materials manually? (y/n)", false);
 }

 if (!importFromCsv || addMoreAfterImport)
 {
 while (true)
 {
 // Allow loading a single item from catalog for convenience
 bool loadFromCatalog = ReadBool(" Load an item from parts catalog? (y/n)", false);
 if (loadFromCatalog)
 {
 // simple selection by part number
 var partNums = LoadCatalogPartNumbers(catalogPath);
 string pn = ReadString(" Enter part number", "");
 if (!string.IsNullOrWhiteSpace(pn) && partNums.Contains(pn))
 {
 var catItem = LoadSingleCatalogEntry(catalogPath, pn);
 if (catItem != null)
 {
 materials.Add(catItem);
 Console.WriteLine($" Added '{catItem.Name}' from catalog.");
 }
 else
 {
 Console.WriteLine(" Part not found in catalog.");
 }
 }
 else
 {
 Console.WriteLine(" Unknown part number.");
 }
 }
 else
 {
 Console.Write("Enter material name (or leave blank to skip): ");
 string? name = Console.ReadLine();
 if (string.IsNullOrWhiteSpace(name)) name = $"Material {materials.Count +1}";

 decimal matCost = ReadDecimal($" Cost of {name} ($)",0m);
 decimal matShip = ReadDecimal($" Shipping/handling for {name} ($)",0m);
 bool customerProvided = ReadBool($" Is the customer bringing/providing {name}? (y/n)", false);
 bool isLarge = ReadBool($" Is {name} a large/bulky item? (y/n)", false);
 decimal largeFee = isLarge ? ReadDecimal($" Large item handling/storage fee for {name} ($)",0m) :0m;
 bool wasteHandled = ReadBool($" Will you handle waste/recycling for {name}? (y/n)", false);
 decimal wasteFee = wasteHandled ? ReadDecimal($" Waste/Recycling fee for {name} ($)",0m) :0m;
 bool taxable = ReadBool($" Taxable item? (y/n)", true);

 bool applyMarginToThis = false;
 if (!customerProvided)
 {
 applyMarginToThis = ReadBool($" Apply margin/markup to {name}? (y/n)", true);
 }

 materials.Add(new MaterialLine
 {
 Name = name,
 Cost = matCost,
 Ship = matShip,
 CustomerProvided = customerProvided,
 IsLarge = isLarge,
 LargeFee = largeFee,
 WasteHandledByShop = wasteHandled,
 WasteFee = wasteFee,
 ApplyMargin = !customerProvided && applyMarginToThis,
 Taxable = taxable
 });
 }

 Console.Write("Done entering materials? (y/n): ");
 string? done = Console.ReadLine()?.Trim().ToLowerInvariant();
 if (done == "y" || done == "yes") break;
 Console.WriteLine();
 }
 }

 Console.WriteLine("\n--- Tax, Labor, and Fees ---");
 decimal taxRatePct = ReadDecimal("Tax rate on parts+shipping (%)",8.5m);
 bool taxLabor = ReadBool("Apply tax to labor/fees? (y/n)", false);

 decimal laborHrs = ReadDecimal("Estimated repair labor hours",3.0m);
 decimal laborRate = ReadDecimal("Labor rate ($/hr)",120m);

 // Processing / Research Fee
 Console.WriteLine("\n--- Processing / Research Fee ---");
 decimal researchHrs = ReadDecimal("Hours spent researching / sourcing materials",1.0m);
 decimal researchRate = ReadDecimal("Admin/processing hourly rate ($/hr)",60m);
 decimal researchFee = researchHrs * researchRate;

 // Shop vehicle storage/handling fee (flat)
 Console.WriteLine("\n--- Shop Fees ---");
 decimal vehicleStorageHandlingFee = ReadDecimal("Vehicle storage/handling fee ($)",0m);

 // Pricing method
 Console.WriteLine("\n--- Pricing Method ---");
 bool useMarkup = ReadBool("Use markup instead of margin? (y/n)", false);
 decimal targetMarginPct = useMarkup ?0m : ReadDecimal("Desired gross margin (%)",35m);
 decimal targetMarkupPct = useMarkup ? ReadDecimal("Desired markup on margin-eligible costs (%)",50m) :0m;

 // Minimum charge policy
 Console.WriteLine("\n--- Minimum Charge Policy ---");
 decimal minInvoicePreTax = ReadDecimal("Minimum invoice charge (pre-tax $)",150m);
 decimal minEffectiveHourly = ReadDecimal("Minimum acceptable effective hourly ($/hr)", laborRate);

 // CALCULATIONS (build components)
 decimal laborCost = laborHrs * laborRate;

 // Totals for billed materials (exclude customer-provided from billed totals)
 decimal billedMaterialsCost =0m;
 decimal billedMaterialsShip =0m;
 decimal handlingFeesTotal =0m;
 decimal wasteFeesTotal =0m;
 decimal materialsMarginEligibleCost =0m; // materials+ship that will be marked up / margined
 decimal materialsPassThroughCost =0m; // materials+ship billed at cost (no margin)
 decimal materialsPassThroughTaxableCost =0m; // taxable subset of pass-through
 decimal materialsMarginEligibleTaxableCost =0m; // taxable subset of margin-eligible

 foreach (var m in materials)
 {
 if (!m.CustomerProvided)
 {
 billedMaterialsCost += m.Cost;
 billedMaterialsShip += m.Ship;
 if (m.ApplyMargin)
 {
 materialsMarginEligibleCost += (m.Cost + m.Ship);
 if (m.Taxable) materialsMarginEligibleTaxableCost += (m.Cost + m.Ship);
 }
 else
 {
 materialsPassThroughCost += (m.Cost + m.Ship);
 if (m.Taxable) materialsPassThroughTaxableCost += (m.Cost + m.Ship);
 }
 }

 if (m.IsLarge && m.LargeFee >0) handlingFeesTotal += m.LargeFee;
 if (m.WasteHandledByShop && m.WasteFee >0) wasteFeesTotal += m.WasteFee;
 }

 // Fees (treated as add-ons, not part of cost base for margin)
 decimal nonMarginAddOns = handlingFeesTotal + wasteFeesTotal + vehicleStorageHandlingFee;

 // Margin-eligible costs also include labor & research
 decimal laborLikeCost = laborCost + researchFee;
 decimal totalMarginEligibleCost = materialsMarginEligibleCost + laborLikeCost;

 // Pass-through costs (no margin)
 decimal passThroughCosts = materialsPassThroughCost;

 // Gross multiplier
 decimal priceMultiplier;
 if (useMarkup)
 {
 priceMultiplier =1m + (targetMarkupPct /100m);
 }
 else
 {
 decimal marginFrac = targetMarginPct /100m;
 priceMultiplier = (marginFrac >=0.999m) ?1m :1m / (1m - marginFrac);
 }

 // Price components
 decimal grossForMarginEligible = Math.Round(totalMarginEligibleCost * priceMultiplier,2);
 // Allocate gross to material vs labor-like proportionally (for tax)
 decimal materialsGrossFromEligible = totalMarginEligibleCost ==0 ?0 : Math.Round(grossForMarginEligible * (materialsMarginEligibleCost / totalMarginEligibleCost),2);
 decimal laborLikeGrossFromEligible = Math.Round(grossForMarginEligible - materialsGrossFromEligible,2);

 decimal priceBeforeTaxRecommended = passThroughCosts + nonMarginAddOns + grossForMarginEligible;

 // Profit at recommended
 decimal costSubtotal = totalMarginEligibleCost + passThroughCosts; // excludes add-on fees (pure revenue)
 decimal profitAtTarget = priceBeforeTaxRecommended - costSubtotal; // includes add-on fees + margin portion
 decimal effectiveMarginPct = priceBeforeTaxRecommended ==0 ?0 : (profitAtTarget / priceBeforeTaxRecommended) *100m;
 decimal equivalentMarkupPct = costSubtotal ==0 ?0 : (profitAtTarget / costSubtotal) *100m;

 // Taxable portion at recommended price
 decimal taxablePassThroughMaterialsPrice = materialsPassThroughTaxableCost;
 decimal taxableMaterialsGrossShare = (materialsMarginEligibleCost ==0 || materialsMarginEligibleTaxableCost ==0)
 ?0m
 : Math.Round(materialsGrossFromEligible * (materialsMarginEligibleTaxableCost / materialsMarginEligibleCost),2);
 decimal taxableMaterialsPrice = taxablePassThroughMaterialsPrice + taxableMaterialsGrossShare;
 decimal taxableLaborLikePrice = taxLabor ? laborLikeGrossFromEligible :0m; // labor & research part of gross
 decimal taxableFeesPrice = taxLabor ? nonMarginAddOns :0m; // handling/waste/vehicle fees follow labor tax
 decimal taxablePreTaxRecommended = taxableMaterialsPrice + taxableLaborLikePrice + taxableFeesPrice;
 decimal customerTaxOnRecommended = Math.Round(taxablePreTaxRecommended * (taxRatePct /100m),2);
 decimal outTheDoorRecommended = priceBeforeTaxRecommended + customerTaxOnRecommended;

 // Apply minimum invoice charge (pre-tax)
 decimal priceBeforeTaxFinal = Math.Max(priceBeforeTaxRecommended, minInvoicePreTax);
 decimal taxableShareOfRecommended = priceBeforeTaxRecommended ==0 ?0 : (taxablePreTaxRecommended / priceBeforeTaxRecommended);
 decimal customerTaxOnFinal = Math.Round(priceBeforeTaxFinal * taxableShareOfRecommended * (taxRatePct /100m),2);
 decimal outTheDoorFinal = priceBeforeTaxFinal + customerTaxOnFinal;

 // Profit at chosen
 decimal profitAtChosen = priceBeforeTaxFinal - costSubtotal;
 decimal effectiveMarginChosenPct = priceBeforeTaxFinal ==0 ?0 : (profitAtChosen / priceBeforeTaxFinal) *100m;
 decimal laborHrsSafe = laborHrs <=0 ?0 : laborHrs;
 decimal profitPerLaborHour = laborHrsSafe >0m ? Math.Round(profitAtChosen / laborHrsSafe,2) : profitAtChosen;

 // Worth-It checks
 decimal fixedNonLaborCosts = billedMaterialsCost + billedMaterialsShip; // research treated as labor-like here
 decimal maxHoursAtMinimum = (minInvoicePreTax > fixedNonLaborCosts && minEffectiveHourly >0)
 ? Math.Round((minInvoicePreTax - fixedNonLaborCosts) / minEffectiveHourly,2)
 :0m;
 decimal maxHoursAtFinal = (priceBeforeTaxFinal > fixedNonLaborCosts && minEffectiveHourly >0)
 ? Math.Round((priceBeforeTaxFinal - fixedNonLaborCosts) / minEffectiveHourly,2)
 :0m;

 // OUTPUT
 Console.WriteLine("\n--- Breakdown ---");
 foreach (var m in materials)
 {
 string flags = string.Empty;
 if (m.CustomerProvided) flags += " [customer-provided]";
 if (!m.ApplyMargin && !m.CustomerProvided) flags += " [no-margin]";
 if (!m.Taxable) flags += " [non-taxable]";
 Console.WriteLine($"• {m.Name,-20} Cost: {(m.CustomerProvided ?0m : m.Cost).ToString("C", ci)}, Shipping: {(m.CustomerProvided ?0m : m.Ship).ToString("C", ci)}{flags}");
 if (m.IsLarge && m.LargeFee >0) Console.WriteLine($" + Large handling/storage fee: {m.LargeFee.ToString("C", ci)}");
 if (m.WasteHandledByShop && m.WasteFee >0) Console.WriteLine($" + Waste/Recycling fee: {m.WasteFee.ToString("C", ci)}");
 }

 Console.WriteLine("------------------------------------------");
 Console.WriteLine($"Parts total (billed): {billedMaterialsCost.ToString("C", ci)}");
 Console.WriteLine($"Shipping total (billed): {billedMaterialsShip.ToString("C", ci)}");
 Console.WriteLine($"Labor: {laborHrs} h @ {laborRate.ToString("C", ci)}/h = {laborCost.ToString("C", ci)}");
 Console.WriteLine($"Research/Admin Fee: {researchFee.ToString("C", ci)}");
 Console.WriteLine($"Handling/Storage fees (per-item): {handlingFeesTotal.ToString("C", ci)}");
 Console.WriteLine($"Waste/Recycling fees: {wasteFeesTotal.ToString("C", ci)}");
 Console.WriteLine($"Vehicle storage/handling fee: {vehicleStorageHandlingFee.ToString("C", ci)}");
 Console.WriteLine($"Cost subtotal (pre-tax, excl. fees): {costSubtotal.ToString("C", ci)}");

 Console.WriteLine("\n--- Target Pricing ---");
 string pricingModeLabel = useMarkup ? $"Markup {targetMarkupPct}%" : $"Margin {targetMarginPct}%";
 Console.WriteLine($"Pricing mode: {pricingModeLabel}");
 if (!useMarkup && targetMarginPct >=99.9m)
 {
 Console.WriteLine("Note:100% gross margin is not feasible. Use markup100% to double cost, or set margin to50%.");
 }
 Console.WriteLine($"Recommended price (pre-tax): {priceBeforeTaxRecommended.ToString("C", ci)}");
 Console.WriteLine($"Estimated tax to charge (recommended): {customerTaxOnRecommended.ToString("C", ci)}");
 Console.WriteLine($"Out-the-door total (recommended): {outTheDoorRecommended.ToString("C", ci)}");
 Console.WriteLine($"Profit at recommended (pre-tax): {profitAtTarget.ToString("C", ci)}");
 Console.WriteLine($"Effective margin (recommended): {effectiveMarginPct:F1}%");
 Console.WriteLine($"Equivalent markup (recommended): {equivalentMarkupPct:F1}%");

 Console.WriteLine("\n--- Final Price (after minimums) ---");
 Console.WriteLine($"Minimum invoice (pre-tax): {minInvoicePreTax.ToString("C", ci)}");
 Console.WriteLine($"Price used (pre-tax): {priceBeforeTaxFinal.ToString("C", ci)}");
 Console.WriteLine($"Estimated tax to charge (final): {customerTaxOnFinal.ToString("C", ci)}");
 Console.WriteLine($"Out-the-door total (final): {outTheDoorFinal.ToString("C", ci)}");
 Console.WriteLine($"Profit at chosen price (pre-tax): {profitAtChosen.ToString("C", ci)}");
 Console.WriteLine($"Effective margin (final): {effectiveMarginChosenPct:F1}%");
 Console.WriteLine($"Profit per labor hour (pre-tax): {profitPerLaborHour.ToString("C", ci)}/h");

 Console.WriteLine("\n--- Worth-It Check ---");
 Console.WriteLine($"Min acceptable effective hourly: {minEffectiveHourly.ToString("C", ci)}/h");
 if (priceBeforeTaxFinal == minInvoicePreTax)
 {
 Console.WriteLine($"Max hours at minimum before underpaid: {maxHoursAtMinimum} h");
 }
 else
 {
 Console.WriteLine("Minimum did not apply (final price > minimum).");
 Console.WriteLine($"Max hours at final price before underpaid: {maxHoursAtFinal} h");
 }

 // === INVOICE / QUOTE EXPORT (TXT) ===
 Console.WriteLine("\n--- Document Export ---");
 string techName = ReadString("Technician name", "");
 string techPhone = ReadString("Technician phone", "");
 string techEmail = ReadString("Technician email", "");
 string customerName = ReadString("Customer name (optional)", "");
 bool isQuote = ReadBool("Is this a quote? (y/n)", true);
 decimal safetyDeposit = ReadDecimal("Safety deposit amount ($)",0m);

 decimal depositApplied = Math.Clamp(safetyDeposit,0m, outTheDoorFinal);
 decimal balanceDueAtCompletion = outTheDoorFinal - depositApplied;

 string docTitle = isQuote ? "QUOTE" : "INVOICE";
 string fileName = $"{docTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
 string filePath = Path.Combine(Environment.CurrentDirectory, fileName);

 var sb = new StringBuilder();
 sb.AppendLine($"================== {docTitle} ==================");
 sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm}");
 if (!string.IsNullOrWhiteSpace(customerName))
 sb.AppendLine($"Customer: {customerName}");
 sb.AppendLine($"Technician: {techName}");
 if (!string.IsNullOrWhiteSpace(techPhone)) sb.AppendLine($"Phone: {techPhone}");
 if (!string.IsNullOrWhiteSpace(techEmail)) sb.AppendLine($"Email: {techEmail}");
 sb.AppendLine();

 sb.AppendLine("-- Line Items --");
 foreach (var m in materials)
 {
 if (m.CustomerProvided)
 {
 sb.AppendLine($"Part (customer-provided): {m.Name} Charge: {0m.ToString("C", ci)}");
 }
 else
 {
 string marginTag = m.ApplyMargin ? "(margin applied)" : "(no margin)";
 sb.AppendLine($"Part: {m.Name} {marginTag} Cost: {m.Cost.ToString("C", ci)} Shipping: {m.Ship.ToString("C", ci)}");
 }
 if (m.IsLarge && m.LargeFee >0)
 sb.AppendLine($" + Large handling/storage fee: {m.LargeFee.ToString("C", ci)}");
 if (m.WasteHandledByShop && m.WasteFee >0)
 sb.AppendLine($" + Waste/Recycling fee: {m.WasteFee.ToString("C", ci)}");
 }
 sb.AppendLine($"Parts total (billed): {billedMaterialsCost.ToString("C", ci)}");
 sb.AppendLine($"Shipping total (billed): {billedMaterialsShip.ToString("C", ci)}");
 sb.AppendLine($"Handling/Storage fees (per-item): {handlingFeesTotal.ToString("C", ci)}");
 sb.AppendLine($"Waste/Recycling fees: {wasteFeesTotal.ToString("C", ci)}");
 sb.AppendLine($"Vehicle storage/handling fee: {vehicleStorageHandlingFee.ToString("C", ci)}");
 sb.AppendLine($"Labor: {laborHrs} h @ {laborRate.ToString("C", ci)}/h = {laborCost.ToString("C", ci)}");
 sb.AppendLine($"Research/Admin: {researchFee.ToString("C", ci)}");
 sb.AppendLine($"Subtotal (pre-tax): {priceBeforeTaxFinal.ToString("C", ci)}");
 sb.AppendLine($"Tax rate: {taxRatePct}%");
 sb.AppendLine(taxLabor ? "Tax applies to labor/fees" : "Tax does not apply to labor/fees");
 sb.AppendLine($"Estimated sales tax: {customerTaxOnFinal.ToString("C", ci)}");
 sb.AppendLine(new string('-',46));
 sb.AppendLine($"Total (out-the-door): {outTheDoorFinal.ToString("C", ci)}");
 sb.AppendLine($"Deposit required: {depositApplied.ToString("C", ci)}");
 sb.AppendLine($"Balance due at completion: {balanceDueAtCompletion.ToString("C", ci)}");
 sb.AppendLine();

 sb.AppendLine("-- Pricing Summary --");
 sb.AppendLine($"Pricing mode: {pricingModeLabel}");
 sb.AppendLine($"Price used (pre-tax): {priceBeforeTaxFinal.ToString("C", ci)}");
 sb.AppendLine($"Profit (pre-tax): {profitAtChosen.ToString("C", ci)}");
 sb.AppendLine($"Effective margin: {effectiveMarginChosenPct:F1}%");
 sb.AppendLine();

 sb.AppendLine("-- Terms & Conditions (Safety Deposit) --");
 sb.AppendLine("- Deposit is applied to the final invoice total.");
 sb.AppendLine("- Special-order parts may be non-returnable; if work is canceled after parts are ordered, deposit may be used to cover parts and admin costs.");
 sb.AppendLine("- If the customer cancels the job after authorization, a portion or all of the deposit may be retained to cover time and materials already incurred.");
 sb.AppendLine("- Vehicle may incur storage fees if not picked up within5 business days after completion.");
 sb.AppendLine("- This document is provided as a general shop policy template; local laws may vary.");
 if (isQuote)
 {
 sb.AppendLine("- This is a quote; pricing subject to change due to parts availability, supplier pricing, or additional findings during repair.");
 }
 sb.AppendLine();

 sb.AppendLine("-- Signatures --");
 sb.AppendLine("Customer: ____________________________ Date: __________");
 sb.AppendLine("Technician: __________________________ Date: __________");

 File.WriteAllText(filePath, sb.ToString());
 Console.WriteLine($"Saved {docTitle.ToLowerInvariant()} to: {filePath}");

 // === INVOICE / QUOTE EXPORT (HTML) ===
 try
 {
 string htmlDir = @"C:\\temp";
 Directory.CreateDirectory(htmlDir);
 string htmlFile = Path.Combine(htmlDir, $"{docTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.html");
 string enc(string s) => WebUtility.HtmlEncode(s ?? string.Empty);

 var html = new StringBuilder();
 html.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
 html.Append("<title>").Append(enc(docTitle)).Append("</title>");
 html.Append("<style>");
 html.Append("body{font-family:Segoe UI,Arial,sans-serif;margin:40px;color:#1a1a1a;background:#f7f7f9;}\n");
 html.Append(".card{max-width:960px;margin:0 auto;background:#fff;border-radius:12px;box-shadow:010px25px rgba(0,0,0,.08);overflow:hidden;}\n");
 html.Append("header{display:flex;justify-content:space-between;align-items:center;padding:28px36px;border-bottom:1px solid #eee;background:linear-gradient(180deg,#fff,#fafafa);}\n");
 html.Append("h1{font-size:24px;margin:0;} .muted{color:#666;}\n");
 html.Append(".grid{display:grid;grid-template-columns:1fr1fr;gap:20px;margin:14px0;}\n");
 html.Append(".section{padding:28px36px;border-bottom:1px solid #f0f0f0;}\n");
 html.Append("table{width:100%;border-collapse:collapse;margin-top:10px;} th,td{padding:12px12px;border-bottom:1px solid #eee;text-align:left;} th{background:#fafafa;font-weight:600;}\n");
 html.Append(".right{text-align:right;} .total{font-weight:700;} .pill{display:inline-block;padding:4px10px;border-radius:999px;background:#eef;border:1px solid #dde;color:#335;}\n");
 html.Append(".footer{padding:28px36px;color:#555;font-size:13px;background:#fafafa;}\n");
 html.Append(".sign{display:flex;gap:28px;margin-top:22px;} .sig{flex:1} .line{border-top:1px solid #aaa;padding-top:8px;margin-top:40px;}\n");
 html.Append("</style></head><body>");
 html.Append("<div class=\"card\">");
 html.Append("<header><div><h1>").Append(enc(docTitle)).Append("</h1>");
 html.Append("<div class=\"muted\">Date: ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("</div>");
 if (!string.IsNullOrWhiteSpace(customerName))
 html.Append("<div class=\"muted\">Customer: ").Append(enc(customerName)).Append("</div>");
 html.Append("</div>");
 html.Append("<div class=\"right\"><div class=\"muted\">Technician</div><div>").Append(enc(techName)).Append("</div>");
 if (!string.IsNullOrWhiteSpace(techPhone)) html.Append("<div class=\"muted\">Phone: ").Append(enc(techPhone)).Append("</div>");
 if (!string.IsNullOrWhiteSpace(techEmail)) html.Append("<div class=\"muted\">Email: ").Append(enc(techEmail)).Append("</div>");
 html.Append("</div></header>");

 html.Append("<div class=\"section\"><div class=\"grid\">");
 html.Append("<div><div class=\"muted\">Pricing Mode</div><div class=\"pill\">").Append(enc(pricingModeLabel)).Append("</div></div>");
 html.Append("<div><div class=\"muted\">Tax</div><div>").Append(taxRatePct.ToString("0.##", ci)).Append("% ").Append(taxLabor ? "(labor/fees taxed)" : "(labor/fees untaxed)").Append("</div></div>");
 html.Append("</div></div>");

 html.Append("<div class=\"section\"><div class=\"muted\">Line Items</div><table><thead><tr><th>Description</th><th class=\"right\">Amount</th></tr></thead><tbody>");
 foreach (var m in materials)
 {
 if (m.CustomerProvided)
 {
 html.Append("<tr><td>Part (customer-provided): ").Append(enc(m.Name)).Append("</td><td class=\"right\">").Append(0m.ToString("C", ci)).Append("</td></tr>");
 }
 else
 {
 string tag = m.ApplyMargin ? "margin applied" : "no margin";
 html.Append("<tr><td>Part: ").Append(enc(m.Name)).Append(" <span class=\"muted\">(").Append(tag).Append(")</span> – Cost ")
 .Append(m.Cost.ToString("C", ci)).Append(", Shipping ").Append(m.Ship.ToString("C", ci)).Append("</td>");
 html.Append("<td class=\"right\">").Append((m.Cost + m.Ship).ToString("C", ci)).Append("</td></tr>");
 }
 if (m.IsLarge && m.LargeFee >0)
 html.Append("<tr><td>Large handling/storage fee – ").Append(enc(m.Name)).Append("</td><td class=\"right\">").Append(m.LargeFee.ToString("C", ci)).Append("</td></tr>");
 if (m.WasteHandledByShop && m.WasteFee >0)
 html.Append("<tr><td>Waste/Recycling fee – ").Append(enc(m.Name)).Append("</td><td class=\"right\">").Append(m.WasteFee.ToString("C", ci)).Append("</td></tr>");
 }
 if (vehicleStorageHandlingFee >0)
 html.Append("<tr><td>Vehicle storage/handling fee</td><td class=\"right\">").Append(vehicleStorageHandlingFee.ToString("C", ci)).Append("</td></tr>");
 html.Append("</tbody></table></div>");

 html.Append("<div class=\"section\"><table><tbody>");
 html.Append("<tr><td>Parts total (billed)</td><td class=\"right\">").Append(billedMaterialsCost.ToString("C", ci)).Append("</td></tr>");
 html.Append("<tr><td>Shipping total (billed)</td><td class=\"right\">").Append(billedMaterialsShip.ToString("C", ci)).Append("</td></tr>");
 html.Append("<tr><td>Labor ").Append(laborHrs.ToString("0.##", ci)).Append(" h @ ").Append(laborRate.ToString("C", ci)).Append("/h</td><td class=\"right\">").Append(laborCost.ToString("C", ci)).Append("</td></tr>");
 html.Append("<tr><td>Research/Admin</td><td class=\"right\">").Append(researchFee.ToString("C", ci)).Append("</td></tr>");
 html.Append("<tr><td>Handling/Storage fees (per-item)</td><td class=\"right\">").Append(handlingFeesTotal.ToString("C", ci)).Append("</td></tr>");
 html.Append("<tr><td>Waste/Recycling fees</td><td class=\"right\">").Append(wasteFeesTotal.ToString("C", ci)).Append("</td></tr>");
 html.Append("<tr><td>Vehicle storage/handling fee</td><td class=\"right\">").Append(vehicleStorageHandlingFee.ToString("C", ci)).Append("</td></tr>");
 html.Append("<tr><td class=\"total\">Subtotal (pre-tax)</td><td class=\"right total\">").Append(priceBeforeTaxFinal.ToString("C", ci)).Append("</td></tr>");
 html.Append("<tr><td>Estimated sales tax</td><td class=\"right\">").Append(customerTaxOnFinal.ToString("C", ci)).Append("</td></tr>");
 html.Append("<tr><td class=\"total\">Total (out-the-door)</td><td class=\"right total\">").Append(outTheDoorFinal.ToString("C", ci)).Append("</td></tr>");
 html.Append("<tr><td>Deposit</td><td class=\"right\">").Append(depositApplied.ToString("C", ci)).Append("</td></tr>");
 html.Append("<tr><td class=\"total\">Balance due at completion</td><td class=\"right total\">").Append(balanceDueAtCompletion.ToString("C", ci)).Append("</td></tr>");
 html.Append("</tbody></table></div>");

 html.Append("<div class=\"section\"><div class=\"muted\">Terms & Conditions (Safety Deposit)</div><ul>");
 html.Append("<li>Deposit is applied to the final invoice total.</li>");
 html.Append("<li>Special-order parts may be non-returnable; if work is canceled after parts are ordered, deposit may be used to cover parts and admin costs.</li>");
 html.Append("<li>If the customer cancels the job after authorization, a portion or all of the deposit may be retained to cover time and materials already incurred.</li>");
 html.Append("<li>Vehicle may incur storage fees if not picked up within5 business days after completion.</li>");
 html.Append("<li>This document is provided as a general shop policy template; local laws may vary.</li>");
 if (isQuote) html.Append("<li>This is a quote; pricing subject to change due to parts availability, supplier pricing, or additional findings during repair.</li>");
 html.Append("</ul>");
 html.Append("<div class=\"sign\"><div class=\"sig\"><div class=\"line\">Customer signature</div></div><div class=\"sig\"><div class=\"line\">Technician signature</div></div></div>");
 html.Append("</div>");

 html.Append("<div class=\"footer\"><span class=\"muted\">Generated by Automotive Repair Pricing Helper</span></div>");
 html.Append("</div></body></html>");

 File.WriteAllText(htmlFile, html.ToString());
 Console.WriteLine($"Saved {docTitle.ToLowerInvariant()} (html) to: {htmlFile}");
 }
 catch (Exception ex)
 {
 Console.WriteLine($"Failed to save HTML export: {ex.Message}");
 }

 // === INVOICE / QUOTE EXPORT (HTML BREAKDOWN WITH BAR GRAPH) ===
 try
 {
 string htmlDir = @"C:\\temp";
 Directory.CreateDirectory(htmlDir);
 string htmlFile2 = Path.Combine(htmlDir, $"{docTitle}_{DateTime.Now:yyyyMMdd_HHmmss}_breakdown.html");
 string enc(string s) => WebUtility.HtmlEncode(s ?? string.Empty);

 // Build category values based on final totals
 decimal materialsCategory = Math.Round(materialsPassThroughCost + materialsGrossFromEligible,2);
 decimal laborCategory = Math.Round(laborLikeGrossFromEligible,2);
 decimal handlingCategory = Math.Round(handlingFeesTotal,2);
 decimal wasteCategory = Math.Round(wasteFeesTotal,2);
 decimal storageCategory = Math.Round(vehicleStorageHandlingFee,2);
 decimal minAdjCategory = Math.Round(Math.Max(0m, priceBeforeTaxFinal - priceBeforeTaxRecommended),2);
 decimal taxesCategory = Math.Round(customerTaxOnFinal,2);

 decimal preTaxSum = materialsCategory + laborCategory + handlingCategory + wasteCategory + storageCategory + minAdjCategory;
 decimal totalForBars = Math.Max(outTheDoorFinal, preTaxSum + taxesCategory);
 decimal pct(decimal val) => totalForBars <=0m ?0m : Math.Min(100m, Math.Round((val / totalForBars) *100m,2));

 string barRow(string label, decimal val, string color)
 {
 var percent = pct(val).ToString("0.##", ci);
 return $"<div class=\"row\"><div class=\"label\">{enc(label)}</div><div class=\"bar\"><div class=\"fill\" style=\"width:{percent}%;background:{color}\"></div></div><div class=\"value\">{val.ToString("C", ci)}<div class=\"muted small\">{percent}%</div></div></div>";
 }

 var chart = new StringBuilder();
 chart.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
 chart.Append("<title>").Append(enc(docTitle)).Append(" Breakdown</title>");
 chart.Append("<style>");
 chart.Append("body{font-family:Segoe UI,Arial,sans-serif;margin:40px;color:#1a1a1a;background:#f7f7f9;}\n");
 chart.Append(".card{max-width:960px;margin:0 auto;background:#fff;border-radius:12px;box-shadow:08px24px rgba(0,0,0,.08);overflow:hidden;}\n");
 chart.Append("header{display:flex;justify-content:space-between;align-items:center;padding:28px36px;border-bottom:1px solid #eee;background:linear-gradient(180deg,#fff,#fafafa);}\n");
 chart.Append("h1{font-size:22px;margin:0;} .muted{color:#666;} .small{font-size:12px;}\n");
 chart.Append(".section{padding:26px36px;border-bottom:1px solid #f0f0f0;}\n");
 chart.Append(".rows{margin-top:8px;} .row{display:flex;align-items:center;gap:12px;margin:10px0;}\n");
 chart.Append(".label{width:210px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}\n");
 chart.Append(".bar{flex:1;height:20px;background:#eef2f8;border-radius:8px;overflow:hidden;position:relative;} .fill{height:100%;border-radius:8px;}\n");
 chart.Append(".value{width:150px;text-align:right;}\n");
 chart.Append(".legend{display:flex;flex-wrap:wrap;gap:12px;margin-top:12px;} .pill{display:inline-flex;align-items:center;gap:8px;padding:4px10px;border-radius:999px;border:1px solid #e3e6ef;background:#fafbff;} .dot{width:10px;height:10px;border-radius:50%;} \n");
 chart.Append(".footer{padding:22px36px;color:#555;font-size:13px;background:#fafafa;}\n");
 chart.Append("</style></head><body>");
 chart.Append("<div class=\"card\">");
 chart.Append("<header><div><h1>").Append(enc(docTitle)).Append(" – Price Breakdown</h1>");
 chart.Append("<div class=\"muted\">Date: ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("</div>");
 if (!string.IsNullOrWhiteSpace(customerName)) chart.Append("<div class=\"muted\">Customer: ").Append(enc(customerName)).Append("</div>");
 chart.Append("</div>");
 chart.Append("<div class=\"right\"><div class=\"muted\">Technician</div><div>").Append(enc(techName)).Append("</div>");
 if (!string.IsNullOrWhiteSpace(techPhone)) chart.Append("<div class=\"muted\">Phone: ").Append(enc(techPhone)).Append("</div>");
 if (!string.IsNullOrWhiteSpace(techEmail)) chart.Append("<div class=\"muted\">Email: ").Append(enc(techEmail)).Append("</div>");
 chart.Append("</div></header>");

 chart.Append("<div class=\"section\"><div class=\"muted\">Totals</div>");
 chart.Append("<div style=\"margin-top:6px\">Pre-tax total: <strong>").Append(priceBeforeTaxFinal.ToString("C", ci)).Append("</strong></div>");
 chart.Append("<div>Sales tax: <strong>").Append(taxesCategory.ToString("C", ci)).Append("</strong></div>");
 chart.Append("<div>Out-the-door total: <strong>").Append(outTheDoorFinal.ToString("C", ci)).Append("</strong></div>");
 chart.Append("</div>");

 chart.Append("<div class=\"section\"><div class=\"muted\">Breakdown</div><div class=\"rows\">");
 chart.Append(barRow("Materials (parts+shipping)", materialsCategory, "#4c78a8"));
 chart.Append(barRow("Labor + Research", laborCategory, "#59a14f"));
 chart.Append(barRow("Per-item Handling (large/bulky)", handlingCategory, "#af7aa1"));
 chart.Append(barRow("Waste/Recycling fees", wasteCategory, "#f28e2b"));
 chart.Append(barRow("Vehicle storage/handling", storageCategory, "#b6992d"));
 if (minAdjCategory >0m) chart.Append(barRow("Minimum invoice adjustment", minAdjCategory, "#2ca9b7"));
 chart.Append(barRow("Sales tax", taxesCategory, "#e45756"));
 chart.Append("</div>");

 chart.Append("<div class=\"legend\">");
 chart.Append("<span class=\"pill\"><span class=\"dot\" style=\"background:#4c78a8\"></span>Materials</span>");
 chart.Append("<span class=\"pill\"><span class=\"dot\" style=\"background:#59a14f\"></span>Labor+Research</span>");
 chart.Append("<span class=\"pill\"><span class=\"dot\" style=\"background:#af7aa1\"></span>Handling</span>");
 chart.Append("<span class=\"pill\"><span class=\"dot\" style=\"background:#f28e2b\"></span>Waste</span>");
 chart.Append("<span class=\"pill\"><span class=\"dot\" style=\"background:#b6992d\"></span>Vehicle storage</span>");
 chart.Append("<span class=\"pill\"><span class=\"dot\" style=\"background:#2ca9b7\"></span>Minimum adj.</span>");
 chart.Append("<span class=\"pill\"><span class=\"dot\" style=\"background:#e45756\"></span>Sales tax</span>");
 chart.Append("</div></div>");

 chart.Append("<div class=\"footer\"><span class=\"muted\">Generated by Automotive Repair Pricing Helper</span></div>");
 chart.Append("</div></body></html>");

 File.WriteAllText(htmlFile2, chart.ToString());
 Console.WriteLine($"Saved {docTitle.ToLowerInvariant()} (html breakdown) to: {htmlFile2}");
 }
 catch (Exception ex)
 {
 Console.WriteLine($"Failed to save breakdown HTML export: {ex.Message}");
 }

 // === SAVE CURRENT PARAMETERS TO CSVs ===
 try
 {
 string ordersDir = Path.Combine(baseCsvDir, "orders");
 Directory.CreateDirectory(ordersDir);
 string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
 string materialsOut = Path.Combine(ordersDir, $"order_{stamp}_materials.csv");
 string settingsOut = Path.Combine(ordersDir, $"order_{stamp}_settings.csv");

 // Write materials CSV
 using (var sw = new StreamWriter(materialsOut, false, new UTF8Encoding(false)))
 {
 sw.WriteLine("name,cost,shipping,customerProvided,isLarge,largeFee,wasteHandled,wasteFee,applyMargin,taxable");
 foreach (var m in materials)
 {
 sw.WriteLine(string.Join(',',
 EscapeCsv(m.Name),
 m.Cost.ToString(CultureInfo.InvariantCulture),
 m.Ship.ToString(CultureInfo.InvariantCulture),
 m.CustomerProvided ? "true" : "false",
 m.IsLarge ? "true" : "false",
 m.LargeFee.ToString(CultureInfo.InvariantCulture),
 m.WasteHandledByShop ? "true" : "false",
 m.WasteFee.ToString(CultureInfo.InvariantCulture),
 m.ApplyMargin ? "true" : "false",
 m.Taxable ? "true" : "false"
));
 }
 }

 // Write settings CSV (key,value)
 using (var sw = new StreamWriter(settingsOut, false, new UTF8Encoding(false)))
 {
 sw.WriteLine("key,value");
 sw.WriteLine($"taxRatePct,{taxRatePct.ToString(CultureInfo.InvariantCulture)}");
 sw.WriteLine($"taxLabor,{(taxLabor ? "true" : "false")}");
 sb.AppendLine($"laborHrs,{laborHrs.ToString(CultureInfo.InvariantCulture)}");
 sb.AppendLine($"laborRate,{laborRate.ToString(CultureInfo.InvariantCulture)}");
 sb.AppendLine($"researchHrs,{researchHrs.ToString(CultureInfo.InvariantCulture)}");
 sb.AppendLine($"researchRate,{researchRate.ToString(CultureInfo.InvariantCulture)}");
 sb.AppendLine($"vehicleStorageHandlingFee,{vehicleStorageHandlingFee.ToString(CultureInfo.InvariantCulture)}");
 sb.AppendLine($"useMarkup,{(useMarkup ? "true" : "false")}");
 sb.AppendLine($"targetMarginPct,{targetMarginPct.ToString(CultureInfo.InvariantCulture)}");
 sb.AppendLine($"targetMarkupPct,{targetMarkupPct.ToString(CultureInfo.InvariantCulture)}");
 sb.AppendLine($"minInvoicePreTax,{minInvoicePreTax.ToString(CultureInfo.InvariantCulture)}");
 sb.AppendLine($"minEffectiveHourly,{minEffectiveHourly.ToString(CultureInfo.InvariantCulture)}");
 }

 Console.WriteLine($"Saved order CSVs to: {ordersDir}");
 }
 catch (Exception ex)
 {
 Console.WriteLine($"Failed to save order CSVs: {ex.Message}");
 }

 Console.WriteLine("\nPress any key to exit...");
 Console.ReadKey();
 }

 // === HELPER FUNCTIONS ===
 static decimal ReadDecimal(string label, decimal defaultVal)
 {
 while (true)
 {
 Console.Write($"{label} [{defaultVal}]: ");
 string? s = Console.ReadLine();
 if (string.IsNullOrWhiteSpace(s)) return defaultVal;
 if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ||
 decimal.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("en-US"), out v))
 {
 if (v >=0) return v;
 }
 Console.WriteLine(" Please enter a non-negative number.");
 }
 }

 static bool ReadBool(string label, bool defaultVal)
 {
 while (true)
 {
 Console.Write($"{label} [{(defaultVal ? "y" : "n")}]: ");
 string? s = Console.ReadLine();
 if (string.IsNullOrWhiteSpace(s)) return defaultVal;
 s = s.Trim().ToLowerInvariant();
 if (s == "y" || s == "yes" || s == "true") return true;
 if (s == "n" || s == "no" || s == "false") return false;
 Console.WriteLine(" Please enter y or n.");
 }
 }

 static int ReadInt(string label, int defaultVal, int min, int max)
 {
 while (true)
 {
 Console.Write($"{label} [{defaultVal}]: ");
 string? s = Console.ReadLine();
 if (string.IsNullOrWhiteSpace(s)) return defaultVal;
 if (int.TryParse(s.Trim(), out int v))
 {
 if (v < min) v = min;
 if (v > max) v = max;
 return v;
 }
 Console.WriteLine(" Please enter a valid integer.");
 }
 }

 static string ReadString(string label, string defaultVal)
 {
 while (true)
 {
 Console.Write($"{label}{(string.IsNullOrEmpty(defaultVal) ? string.Empty : $" [{defaultVal}]")}: ");
 string? s = Console.ReadLine();
 if (string.IsNullOrWhiteSpace(s)) return defaultVal;
 return s.Trim();
 }
 }

 static void EnsureCsvTemplate(string templatePath)
 {
 try
 {
 if (!File.Exists(templatePath))
 {
 Directory.CreateDirectory(Path.GetDirectoryName(templatePath)!);
 using var sw = new StreamWriter(templatePath, false, new UTF8Encoding(false));
 sw.WriteLine("name,cost,shipping,customerProvided,isLarge,largeFee,wasteHandled,wasteFee,applyMargin,taxable");
 sw.WriteLine("Alternator,150,15,false,false,0,false,0,true,true");
 sw.WriteLine("Bumper,200,25,false,true,20,true,5,true,true");
 sw.WriteLine("Customer Oil,0,0,true,false,0,false,0,false,true");
 }
 }
 catch (Exception ex)
 {
 Console.WriteLine($"Could not create template CSV: {ex.Message}");
 }
 }

 static void EnsurePartsCatalog(string catalogPath)
 {
 try
 {
 if (!File.Exists(catalogPath))
 {
 using var sw = new StreamWriter(catalogPath, false, new UTF8Encoding(false));
 sw.WriteLine("partNumber,description,cost,shipping,taxable,isLarge,largeFee,wasteHandled,wasteFee,applyMargin");
 }
 }
 catch (Exception ex)
 {
 Console.WriteLine($"Could not create parts catalog: {ex.Message}");
 }
 }

 static HashSet<string> LoadCatalogPartNumbers(string catalogPath)
 {
 var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 if (!File.Exists(catalogPath)) return set;
 using var sr = new StreamReader(catalogPath, Encoding.UTF8, true);
 string? header = sr.ReadLine();
 while (!sr.EndOfStream)
 {
 var line = sr.ReadLine();
 if (string.IsNullOrWhiteSpace(line)) continue;
 var cols = SplitCsv(line);
 if (cols.Count >0)
 {
 set.Add(cols[0].Trim());
 }
 }
 return set;
 }

 static void AppendCatalogEntry(string catalogPath, string partNumber, string description, decimal cost, decimal shipping, bool taxable, bool isLarge, decimal largeFee, bool wasteHandled, decimal wasteFee, bool applyMargin)
 {
 try
 {
 using var sw = new StreamWriter(catalogPath, true, new UTF8Encoding(false));
 sw.WriteLine(string.Join(',',
 EscapeCsv(partNumber),
 EscapeCsv(description),
 cost.ToString(CultureInfo.InvariantCulture),
 shipping.ToString(CultureInfo.InvariantCulture),
 taxable ? "true" : "false",
 isLarge ? "true" : "false",
 largeFee.ToString(CultureInfo.InvariantCulture),
 wasteHandled ? "true" : "false",
 wasteFee.ToString(CultureInfo.InvariantCulture),
 applyMargin ? "true" : "false"
));
 }
 catch (Exception ex)
 {
 Console.WriteLine($"Failed to append to catalog: {ex.Message}");
 }
 }

 static MaterialLine? LoadSingleCatalogEntry(string catalogPath, string partNumber)
 {
 if (!File.Exists(catalogPath)) return null;
 using var sr = new StreamReader(catalogPath, Encoding.UTF8, true);
 string? header = sr.ReadLine();
 while (!sr.EndOfStream)
 {
 string? line = sr.ReadLine();
 if (string.IsNullOrWhiteSpace(line)) continue;
 var cols = SplitCsv(line);
 if (cols.Count <10) continue;
 if (string.Equals(cols[0].Trim(), partNumber, StringComparison.OrdinalIgnoreCase))
 {
 return new MaterialLine
 {
 PartNumber = cols[0].Trim(),
 Name = cols[1].Trim(), // description used on invoice
 Cost = ParseDecimal(cols[2]),
 Ship = ParseDecimal(cols[3]),
 Taxable = ParseBool(cols[4]),
 IsLarge = ParseBool(cols[5]),
 LargeFee = ParseDecimal(cols[6]),
 WasteHandledByShop = ParseBool(cols[7]),
 WasteFee = ParseDecimal(cols[8]),
 ApplyMargin = ParseBool(cols[9]),
 CustomerProvided = false
 };
 }
 }
 return null;
 }

 static List<MaterialLine> LoadMaterialsFromCsv(string path)
 {
 var list = new List<MaterialLine>();
 using var sr = new StreamReader(path, Encoding.UTF8, true);
 string? header = sr.ReadLine();
 if (header == null) return list;
 var headerCols = SplitCsv(header);
 var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
 for (int i =0; i < headerCols.Count; i++)
 {
 map[headerCols[i].Trim()] = i;
 }
 bool hasName = map.ContainsKey("name");
 bool hasDesc = map.ContainsKey("description");
 bool hasPart = map.ContainsKey("partNumber");

 while (!sr.EndOfStream)
 {
 string? line = sr.ReadLine();
 if (string.IsNullOrWhiteSpace(line)) continue;
 var cols = SplitCsv(line);
 string name = hasName ? cols[map["name"]] : (hasDesc ? cols[map["description"]] : "");
 if (string.IsNullOrWhiteSpace(name)) name = "Item";
 var m = new MaterialLine
 {
 Name = name.Trim(),
 Cost = GetColDecimal(cols, map, "cost",0m),
 Ship = GetColDecimal(cols, map, "shipping",0m),
 CustomerProvided = GetColBool(cols, map, "customerProvided", false),
 IsLarge = GetColBool(cols, map, "isLarge", false),
 LargeFee = GetColDecimal(cols, map, "largeFee",0m),
 WasteHandledByShop = GetColBool(cols, map, "wasteHandled", false),
 WasteFee = GetColDecimal(cols, map, "wasteFee",0m),
 ApplyMargin = GetColBool(cols, map, "applyMargin", true),
 Taxable = GetColBool(cols, map, "taxable", true),
 PartNumber = hasPart ? cols[map["partNumber"]].Trim() : null
 };
 list.Add(m);
 }
 return list;
 }

 static decimal GetColDecimal(List<string> cols, Dictionary<string, int> map, string key, decimal def)
 {
 if (map.TryGetValue(key, out int idx) && idx >=0 && idx < cols.Count)
 return ParseDecimal(cols[idx]);
 return def;
 }
 static bool GetColBool(List<string> cols, Dictionary<string, int> map, string key, bool def)
 {
 if (map.TryGetValue(key, out int idx) && idx >=0 && idx < cols.Count)
 return ParseBool(cols[idx]);
 return def;
 }

 static decimal ParseDecimal(string s)
 {
 if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
 if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("en-US"), out d)) return d;
 return 0m;
 }

 static bool ParseBool(string s)
 {
 if (string.IsNullOrWhiteSpace(s)) return false;
 s = s.Trim().ToLowerInvariant();
 return s == "y" || s == "yes" || s == "true" || s == "1";
 }

 static string EscapeCsv(string s)
 {
 if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
 {
 return '"' + s.Replace("\"", "\"\"") + '"';
 }
 return s;
 }

 static List<string> SplitCsv(string line)
 {
 var result = new List<string>();
 var sb = new StringBuilder();
 bool inQuotes = false;
 for (int i =0; i < line.Length; i++)
 {
 char c = line[i];
 if (inQuotes)
 {
 if (c == '"')
 {
 if (i +1 < line.Length && line[i +1] == '"')
 {
 sb.Append('"');
 i++;
 }
 else
 {
 inQuotes = false;
 }
 }
 else
 {
 sb.Append(c);
 }
 }
 else
 {
 if (c == ',')
 {
 result.Add(sb.ToString());
 sb.Clear();
 }
 else if (c == '"')
 {
 inQuotes = true;
 }
 else
 {
 sb.Append(c);
 }
 }
 }
 result.Add(sb.ToString());
 return result;
 }
}
