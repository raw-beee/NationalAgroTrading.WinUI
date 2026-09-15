# NationalAgroTrading.WinUI — ERP Improvement Plan

Working list of issues to fix before this becomes the official internal ERP.
Process: fix one issue at a time → build → verify → check the box.

---

## Critical — data safety & correctness

| # | Issue | Location | Suggested fix |
|---|-------|----------|---------------|
| 1 | Hardcoded DB path `D:\National Software\natdatabase.db` | `Data/DatabaseHelper.cs:10-11` | Load path from `appsettings.json` next to exe (or `%ProgramData%\NationalAgroTrading\config.json`), defaulting to current path. One `AppConfig` static class loaded at startup. |
| 2 | Purchase save: no transaction, each item = 4-5 separate connections | `Views/Purchase/AddPurchasePage.xaml.cs:522-740` | Copy sales pattern: one connection + `BeginTransaction()`, pass `conn, transaction` into every write, Commit/Rollback in try/catch. |
| 3 | Purchase update: no transaction (phases can half-fail) | `Views/Purchase/UpdatePurchasePage.xaml.cs:854-1303` | Wrap delete/update/insert phases in one transaction; verify affected-row counts before commit. |
| 4 | Purchase delete: rows deleted on separate connections | `Views/Purchase/BillDetailsPage.xaml.cs:222-384` | Same transaction pattern + warn if sales exist against the bill before deleting. |
| 5 | `last_insert_rowid()` on new connection → always 0, breaks `CostPriceHistory.ReferencePurchaseID` | `AddPurchasePage.xaml.cs:614-620`, `UpdatePurchasePage.xaml.cs:1250-1257` | Append `SELECT last_insert_rowid();` to the INSERT command itself (same connection), like `AddSalePage.xaml.cs:1155` does. |
| 6 | Payment save: Payment + Ledger insert on separate connections | `Views/Payment/AddPaymentPage.xaml.cs:228-356` | One transaction around both inserts. |
| 7 | Payment delete matches Ledger rows via `LIKE '%receipt%'` — empty receipt deletes ALL rows for company; no transaction | `Views/Payment/PaymentPage.xaml.cs:199-248` | Add `PaymentID` column to `Ledger`, delete by `WHERE PaymentID = @id`, wrap in transaction. |
| 8 | VAT computed inconsistently (purchase stores pre-discount VAT, sales uses post-discount) | `Views/Purchase/PurchaseModels.cs:75-78`, `AddPurchasePage.xaml.cs:553-609` vs `AddSalePage.xaml.cs:1137-1149` | One rule: VAT = 13% × after-discount. Shared `TaxCalculator` static class used by all 4 call sites. |
| 9 | Hardcoded bills folder | `Views/Sales/SalesBillPdfGenerator.cs:66` | Same config as #1; fallback to `Documents\National Agro Trading\Sales Bills`. |

## High — trust & control

| # | Issue | Location | Suggested fix |
|---|-------|----------|---------------|
| 10 | No unique bill numbers (purchases have no check at all) | `AddPurchasePage.xaml.cs:464-748`, `AddSalePage.xaml.cs:1503-1532` | `CREATE UNIQUE INDEX` on Sales.BillNumber and Purchase.BillNumber (store raw number, prefix only for display); catch unique violation → friendly message. |
| 11 | Stock overwrite with no audit; opening-qty box copies into all 3 fields | `Views/Inventory/UpdateInventoryPage.xaml.cs:106-133, 45-67` | Add `StockAdjustment` table (ProductID, OldQty, NewQty, Reason, UserName, Timestamp); make only TotalStockLeft directly settable. |
| 12 | Purchase edit/delete can drive stock negative | `UpdatePurchasePage.xaml.cs:969-988, 1100-1122` | Check `TotalStockLeft >= qty` before decrementing; refuse with clear message (same guard as sales). |
| 13 | Silent empty catches: VAT→false, price→0, sale date→today | `Views/Sales/UpdateSalePage.xaml.cs:363-366, 916-919, 1879-1882` | Throw (fail loudly) on save-path lookups; log + fallback only for display-only lookups. |
| 14 | No logging anywhere | whole project | Tiny `Logger.Append(msg)` → `%ProgramData%\NationalAgroTrading\logs\yyyy-MM.log`; call in every catch. |
| 15 | UpdatePaymentPage stub — Update button does nothing | `Views/Payment/UpdatePaymentPage.xaml.cs:298-321` | Wire Update: recompute Ledger row in same transaction as Payment update; hide button until implemented. |
| 16 | No login / users / roles / CreatedBy | `App.xaml.cs:44-48` | `User` table (PBKDF2 hash, role Admin/Staff), login page before MainWindow, static `CurrentUser`, stamp `CreatedBy` on transactions, admin-only destructive ops. |
| 17 | All DB calls sync on UI thread; per-row VAT lookup loading a bill | `UpdateSalePage.xaml.cs:301-303` and everywhere | First: date-filter lists to selected fiscal year (also fixes #28). Then `Task.Run` for page loads. |

## Medium — maintainability & polish

| # | Issue | Location | Suggested fix |
|---|-------|----------|---------------|
| 18 | `SaleService.cs` (772 lines) is dead code, VAT math disagrees with pages | `Services/SaleService.cs:212-625` | Delete it. |
| 19 | SQL copy-paste (Sales INSERT 4×, GetStock 4×) | `AddSalePage.xaml.cs:1155`, `UpdateSalePage.xaml.cs:1642`, `SaleService.cs:301, 508` | After #2-5: move shared writes into one service, pass conn+transaction in. |
| 20 | `Ledger` table (payments) vs LedgerPage derived ledger — opposite sign conventions | `AddPaymentPage.xaml.cs:325-355` vs `LedgerPage.xaml.cs:361-385` | Keep derived ledger as source of truth; stop writing `Ledger` table; migrate history first. |
| 21 | BS dates as text, legacy rows in `M/d/yyyy` break ordering/ranges | `LedgerPage.xaml.cs:233-237`, `UpdatePurchasePage.xaml.cs:285-298` | One-time backup + SQL cleanup to `yyyy-MM-dd`; `NormalizeDate()` helper on every write. |
| 22 | Raw unvalidated date-picker text saved as Nepali date | `AddPurchasePage.xaml.cs:524-529`, `AddPaymentPage.xaml.cs:204-209` | Block save when `IsDateValid == false`; `SelectedBSDate` returns null when invalid. |
| 23 | No rounding before storing totals | `AddSalePage.xaml.cs:1062`, `UpdateSalePage.xaml.cs:1336` | `Math.Round(v, 2, MidpointRounding.AwayFromZero)` in shared calculator (#8). |
| 24 | Ledger export buttons always throw `NotImplementedException` | `Views/Ledger/LedgerPage.xaml.cs:833-839` | Store window instance in `App.OnLaunched`, pass real window to picker. |
| 25 | Expenses PDF export is placeholder dialog | `Views/Expenses/ExpensesPage.xaml.cs:1206-1214` | Reuse ClosedXML → Excel export first; PDF later. |
| 26 | Counter Sales button doesn't navigate (no page) | `MainWindow.xaml.cs:56-63` | Implement page (customer-sale logic exists) or hide button until ready. |
| 27 | Duplicate SQLite NuGet packages (only System.Data.SQLite used) | `NationalAgroTrading.WinUI.csproj` | Remove `Microsoft.Data.Sqlite.Core`. |
| 28 | Unbounded full-table loads | `SaleService.cs:627-758`, `PaymentPage.xaml.cs:36-52` | Covered by #17 fiscal-year filter. |

## Extra quality wins (no issue number — add when convenient)

- [ ] **Backup**: copy DB to `Backups\yyyy-MM-dd.db` daily on close, keep last 30. Do together with #1.
- [ ] **Dashboard on real data**: stock value (qty × cost), receivables, this-month vs last-month sales.
- [ ] **Confirm dialogs include item names** ("Delete INV-12 from Kalika Traders?") on all deletes.
- [ ] **Fiscal-year selector in title bar** respected by all list pages.

## Recommended order

1 (+backup, 9, 27) → 2/3/4/5 → 6/7 → 8/23 → 10/11/12 → 13/14 → 16 → 17/28 → polish (18-26)

## Status log

| Date | Issue # | What was done | Verified how |
|------|---------|---------------|--------------|
| 2026-09-09 | 1 | New `Services/AppConfig.cs` — reads `C:\ProgramData\NationalAgroTrading\config.json` (or `config.json` next to exe), auto-creates it with defaults on first run. Defaults preserve the old `D:\National Software\natdatabase.db` path. `DatabaseHelper.GetConnection()` now throws a clear error if the DB file is missing instead of letting SQLite create an empty one. | Build: 0 errors |
| 2026-09-09 | 9 | `SalesBillPdfGenerator.PrimaryBillsFolder` now reads `AppConfig.SalesBillsFolder` (config key `SalesBillsFolder`). | Build: 0 errors |
| 2026-09-09 | 27 | Removed unused `Microsoft.Data.Sqlite.Core` package reference from csproj. | Build: 0 errors, restore OK |
| 2026-09-09 | Bonus | New `Services/BackupManager.cs` — daily DB backup (on app start + window close) to `Documents\National Agro Trading\Backups` (config key `BackupFolder`), keeps last 30 copies, failures logged to `backup-log.txt` in that folder. | Build: 0 errors |
| 2026-09-09 | 2 | `AddPurchasePage.SavePurchaseButton_Click` — whole save loop (purchase insert + cost history + inventory + product cost, per item) now runs on one connection + transaction with rollback. Also: `SELECT last_insert_rowid()` appended to the INSERT on the same connection (fixes the purchaseId=0 half of #5 on this page), and a missing inventory row now aborts the save instead of passing silently. | Build: 0 errors |
| 2026-09-09 | 3 | `UpdatePurchasePage.SavePurchaseChanges` — delete/update/insert phases + cost-history helpers (`UpdateCostPriceHistory`, `InsertCostPriceHistoryIfRequired`, `RestoreLatestProductCost`) all run on one connection + transaction with rollback. New-item INSERT now fetches its ID on the same connection (other half of #5). | Build: 0 errors |
| 2026-09-09 | 4 | `Purchase/BillDetailsPage.DeletePurchaseBill` — inventory decrement, history delete, purchase delete, and product-cost restore all in one transaction with rollback. Added stock guard: refuses deletion when remaining stock is less than the purchased quantity (units already sold), instead of driving stock negative. | Build: 0 errors |
| 2026-09-09 | 5 | Fixed as part of #2/#3 (both purchase write paths now get the real PurchaseID on the same connection). Note: old rows already stored with ReferencePurchaseID = 0 are NOT repaired — a one-time data cleanup is still needed if history accuracy matters. | Build: 0 errors |
| 2026-09-09 | 6 | `AddPaymentPage.SavePayment` — Payment insert, ledger-balance read, and Ledger insert now run in one transaction with rollback. New PaymentID is captured on the same connection (`last_insert_rowid` appended to the INSERT) and stored on the Ledger row. | Build: 0 errors |
| 2026-09-09 | 7 | New `DatabaseHelper.EnsureLedgerPaymentIdColumn()` runs once at app startup: adds `Ledger.PaymentID` column (idempotent) and best-effort backfills old rows by exact match (company + amount + date + full Particular text). `PaymentPage.DeletePayment` now deletes the ledger row by exact `PaymentID` inside one transaction — the `LIKE '%receipt%'` text matching (which could wipe a company's whole ledger on an empty receipt) is gone. Known limitations: (a) old ledger rows that can't be backfilled stay orphaned when their payment is deleted — safer than deleting wrong rows; (b) the stored running `Balance` column is not recalculated after a delete (pre-existing behaviour, ties into #20). | Build: 0 errors; migration SQL not dry-run against live DB (no sqlite tooling) — idempotent + startup try/catch |
| 2026-09-09 | 8 (+23) | New `Services/TaxCalculator.cs` — the single source of truth: bill discount allocated pro-rata per item, VAT 13% on the AFTER-discount amount, all amounts rounded to 2 dp (AwayFromZero). Routed through it: `AddSalePage` (display + save), `UpdateSalePage` (grid, display + save), `SalesBillPdfGenerator`, `AddPurchasePage` (preview, display + save — **was storing pre-discount VAT; now post-discount, so purchases finally match the displayed total**), `UpdatePurchasePage` + `EditablePurchaseItem`, `PurchaseEntryItem`, `SaleEntryItem`, `EditableSaleItem`, `SaleItem`. Bill totals now reconcile: displayed = stored = printed, since `BillVat` sums the same rounded per-item amounts that get stored. Only remaining `0.13m` literals: TaxCalculator itself and dead `SaleService` (#18). | Build: 0 errors |
| 2026-09-11 | 10 | Per-fiscal-year unique bill numbers. New `DatabaseHelper.FiscalYearOf(nepaliDate)` (Shrawan-start rule; null for unparseable dates) and `EnsureBillFiscalYearSupport()` startup migration: adds `FiscalYear` column to Sales/Purchase, backfills from NepaliDate, creates `idx_sales_bill_fy` (BillNumber+FiscalYear) and `idx_purchase_company_bill_fy` (CompanyID+BillNumber+FiscalYear) as **partial indexes excluding existing duplicate groups** — legacy test-data duplicates are untouched and don't block the migration; every save after this IS enforced. All 4 save paths stamp FiscalYear; sales + purchases get pre-save duplicate checks scoped to company+fiscal year; UNIQUE-violation catches show friendly "already exists in fiscal year X" messages. | Dry-run on a live-DB copy: 155 Sales + 40 Purchase backfilled (0 NULL), indexes created, dup-in-same-FY rejected / same-number-diff-FY allowed, ProgramData config restored. Build: 0 errors. Legacy duplicates still excluded from index — recreate index without exclusion after cleaning test data |
