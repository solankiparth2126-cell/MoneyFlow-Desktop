# Changelog

All notable changes to the MoneyFlow Desktop Accounting application will be documented in this file.

## [Phase 18: Trial Balance Report] - 2026-09-10
### Added
- Created `TrialBalanceForm`:
  - Full Tally-inspired Section 18, 30 double-entry reconciliation Trial Balance interface.
  - Multi-Perspective View Modes: Instant toggle between Detailed (Ledger-wise) and Condensed (Group-wise) (`F1`).
  - Date Range Filtering: `From Date (F2)` and `To Date` pickers constrained to active Financial Year bounds.
  - 6-Column Double-Entry Matrix: Opening Debit/Credit, Period Transactions Debit/Credit, and Closing Debit/Credit.
  - Mathematical Integrity Verification: Validates that total closing debits match total closing credits, displaying a green `[ ✔ ] BALANCED (Diff: ₹0.00)` banner or alert difference.
  - Drill-Down Navigation: Double-click or `Enter` on any ledger row opens its full Ledger Statement (`LedgerStatementForm`).
  - Search & Export: Real-time text search, RFC 4180 CSV export, and print preview (`Ctrl+P`).
- Integrated into `MainForm`:
  - Wired menu item `Reports -> &Trial Balance` to launch `TrialBalanceForm`.
  - Wired Gateway of Accounting list item `Trial Balance` to launch `TrialBalanceForm`.
  - Registered `TrialBalanceForm` in Dependency Injection in `Program.cs`.
- Generated desktop UI design screen in Stitch MCP (`projects/10546831619592911675/screens/9753586d61104fcc8e85dd2aa4756281`).
- Added automated unit tests in `Phase18TrialBalanceTests.cs`:
  - Full reconciliation across diverse voucher types (Purchases, Sales, Payments, Receipts, Contra).
  - Mathematical identity check (`NetClosing == NetOpening + NetPeriod`) across every ledger.
  - Soft-deleted voucher exclusion from Trial Balance figures.
  - 3/3 new tests passing (90/90 total tests passing across all test suites).

## [Phase 17: Ledger Statement Report] - 2026-09-10
### Added
- Created `LedgerStatementForm`:
  - Full Tally-inspired Section 16, 29, 30 detailed Ledger Statement / Account Extract interface.
  - Fast Ledger Selection: Searchable dropdown (`_cmbLedger`, `F4`) listing all company ledgers alphabetically.
  - Date Range Filtering: `From Date (F2)` and `To Date` pickers constrained to active Financial Year.
  - Ledger Context & Opening Balance Card: Displays ledger name, group hierarchy, and dynamic opening balance as of `From Date` (`₹XX,XXX.XX Dr/Cr`).
  - Running Balance DataGridView: Date, Particulars (opposing contra ledger account), Voucher Type, Voucher No, Debit (₹), Credit (₹), Running Balance (₹ with Dr/Cr indicator), and Narration.
  - Explicit Opening Balance Row: Injects row 0 indicating brought-forward balance (`** Opening Balance **`).
  - Audit Summary Footer: Displays Period Debit Total, Period Credit Total, and Net Closing Balance (`₹XX,XXX.XX Dr/Cr`).
  - Transaction Drill-Down: Double-click or `Enter` on any row opens detailed voucher information modal.
  - Export & Print: Full CSV export with RFC 4180 escaping, print preview (`Ctrl+P`), refresh (`F5`), and quick close (`Esc`).
- Integrated into `MainForm`:
  - Wired menu item `Reports -> &Ledger Statement` to launch `LedgerStatementForm`.
  - Wired Gateway of Accounting list item `Ledger Statement` to launch `LedgerStatementForm`.
  - Registered `LedgerStatementForm` in Dependency Injection in `Program.cs`.
- Generated desktop UI design screen in Stitch MCP (`projects/10546831619592911675/screens/6e558dab2e4d4255920b0e099214ea74`).
- Added automated unit tests in `Phase17LedgerStatementTests.cs`:
  - Dynamic opening balance calculation from prior period transactions.
  - Running balance tracking and opposing contra particulars resolution across sequential transactions.
  - Soft-deleted voucher exclusion from opening, period lines, and closing balances.
  - 3/3 new tests passing (87/87 total tests passing across all test suites).

## [Phase 16: Day Book Report] - 2026-09-10
### Added
- Created `DayBookForm`:
  - Full Tally-inspired Section 16, 29, 30 Day Book interface for chronological transaction auditing across all 8 voucher types (Payment, Receipt, Contra, Journal, Sales, Purchase, Debit Note, Credit Note).
  - Date Range filtering: `From Date (F2)` and `To Date` constrained to active Financial Year.
  - Voucher Type filtering: Dropdown filter to view all vouchers or isolate specific types (Contra, Payment, Receipt, Journal, Sales, Purchase, Debit Note, Credit Note).
  - Live Text Search: Instant client-side filtering across Voucher Numbers, Voucher Types, Particulars / Account Names, Reference Numbers, and Narrations.
  - DataGridView: Date, Type, Voucher No, Ref No, Particulars, Debit Amount (₹), Credit Amount (₹), and Narration with alternating row highlights and right-aligned currency formatting.
  - Voucher Drill-Down: Double-click or `Enter` on any row opens complete voucher audit detail.
  - Summary Panel: Live transaction counter, Total Debit (₹), Total Credit (₹), and double-entry `[ ✔ ] BALANCED` check verification banner.
  - Export & Print: CSV export feature with RFC 4180 CSV escaping, print preview (`Ctrl+P`), and keyboard shortcuts (`F2`, `F4`, `F5`, `Esc`).
- Enhanced `IAccountingService` and `AccountingService`:
  - Added `GetDayBookAsync`: Queries vouchers in chronological order with eager loading of voucher types and ledger line items.
  - Generates comprehensive Dr/Cr particulars summaries, debit/credit totals, and balance checks.
  - Added `DayBookItemDto` and `DayBookReportDto` to `MoneyFlow.Core/DTOs`.
- Integrated into `MainForm`:
  - Wired menu item `Reports -> &Day Book` to launch `DayBookForm`.
  - Wired Gateway of Accounting list item `Day Book` to launch `DayBookForm`.
  - Registered `DayBookForm` in Dependency Injection in `Program.cs`.
- Generated desktop UI design screen in Stitch MCP (`projects/10546831619592911675/screens/9f5395da99144edaaef0c9a56714292a`).
- Added automated unit tests in `Phase16DayBookTests.cs`:
  - Chronological transaction ordering across date ranges with multiple voucher types.
  - Voucher type filtering (Sales vs Receipt).
  - Soft-deleted voucher exclusion from Day Book reports.
  - 3/3 new tests passing (84/84 total tests passing across all test suites).

## [Phase 15: Credit Note (Ctrl+F8)] - 2026-09-10
### Added
- Created `CreditNoteForm`:
  - Full Tally-inspired Section 16/29/67 Credit Note interface for Customer Sales Returns, credit allowances, and damaged goods adjustments with strictly ZERO GST.
  - Header with dynamic Voucher Number preview (`CRN-00001`), Voucher Date with active FY constraints, Original Sales Invoice / Reference Number, and Customer Party selector (Sundry Debtors, Cash, Bank) with live balance indicator.
  - Sales Return / Income selector (Sales Returns, Sales Accounts, Direct Income) with live balance indicator.
  - Line items DataGridView: Item / Description, Return Quantity, Return Rate (₹), Return Amount (₹), and Reason / Narration.
  - Automatic dynamic line-item recalculation on quantity and rate changes.
  - Summary panel tracking Total Return Amount and dynamic live balance impact.
  - Automatic balanced double-entry generation on save: Sales Return Dr (reduces revenue) and Customer Party Cr (reduces debtor receivable/debt or cash refund).
  - Action buttons and keyboard shortcuts: Save (`Ctrl+A` / `Enter`), Save & New (`Alt+S`), Clear (`Alt+N`), Print (`Ctrl+P`), and Cancel (`Esc`).
- Integrated into `MainForm`:
  - Wired `Ctrl+F8` global shortcut key to launch `CreditNoteForm`.
  - Added toolbar button `Credit Note (Ctrl+F8)`, menu item `Transactions -> &Credit Note (Ctrl+F8)`, and Gateway list item `Credit Note (Ctrl+F8)`.
  - Registered `CreditNoteForm` in Dependency Injection in `Program.cs`.
- Generated desktop UI design screen in Stitch MCP (`projects/10546831619592911675/screens/c2659c66c4984473a9cb94b20cf2921d`).
- Added automated unit tests in `Phase15CreditNoteTests.cs`:
  - Customer sales return against credit debtor (Sales Returns Dr, Sundry Debtor Cr) reducing customer receivable balance.
  - Immediate cash refund credit note (Sales Returns Dr, Cash Cr).
  - Credit note voucher deletion with dynamic restoration of original customer receivable balance.
  - Zero-sum round-trip test: Sales Invoice (SLS) followed by complete Credit Note (CRN) return restores net customer balance to zero.
  - 4/4 new tests passing (81/81 total tests passing across all test suites).

## [Phase 14: Debit Note (Ctrl+F9)] - 2026-09-10
### Added
- Created `DebitNoteForm`:
  - Full Tally-inspired Section 16/29/67 Debit Note interface for Purchase Returns, supplier price adjustments, and damaged goods returns with strictly ZERO GST.
  - Header with dynamic Voucher Number preview (`DBN-00001`), Voucher Date with active FY constraints, Original Purchase Invoice / Reference Number, and Supplier Party selector (Sundry Creditors, Cash, Bank) with live balance indicator.
  - Return / Account selector (Purchase Returns, Purchase Accounts, Direct Expenses) with live balance indicator.
  - Line items DataGridView: Item / Description, Return Quantity, Return Rate (₹), Return Amount (₹), and Reason / Narration.
  - Automatic dynamic line-item recalculation on quantity and rate changes.
  - Summary panel tracking Total Return Amount and dynamic live balance impact.
  - Automatic balanced double-entry generation on save: Supplier Party Dr (reduces supplier payable/refunds cash) and Purchase Returns / Expense Cr.
  - Action buttons and keyboard shortcuts: Save (`Ctrl+A` / `Enter`), Save & New (`Alt+S`), Clear (`Alt+N`), Print (`Ctrl+P`), and Cancel (`Esc`).
- Integrated into `MainForm`:
  - Wired `Ctrl+F9` global shortcut key to launch `DebitNoteForm`.
  - Added toolbar button `Debit Note (Ctrl+F9)`, menu item `Transactions -> &Debit Note (Ctrl+F9)`, and Gateway list item `Debit Note (Ctrl+F9)`.
  - Registered `DebitNoteForm` in Dependency Injection in `Program.cs`.
- Added automated unit tests in `Phase14DebitNoteTests.cs`:
  - Purchase return against credit supplier (Sundry Creditor Dr, Purchase Returns Cr) reducing supplier payable balance.
  - Immediate cash refund debit note (Cash Dr, Purchase Returns Cr).
  - Debit note voucher deletion with dynamic restoration of original supplier liability.
  - Zero-sum round-trip test: Purchase Invoice (PUR) followed by complete Debit Note (DBN) return restores net supplier balance to zero.
  - 4/4 new tests passing (77/77 total tests passing across all test suites).

## [Phase 13: Purchase Voucher (F9)] - 2026-09-10
### Added
- Created `PurchaseVoucherForm`:
  - Full Tally-inspired Section 25 Purchase Invoice interface with pure accounting calculations and strictly ZERO GST.
  - Header with dynamic Voucher Number preview (`PUR-00001`), Voucher Date with active FY constraints, Supplier Invoice / Reference Number, and Supplier Party selector (Sundry Creditors, Cash, Bank) with live balance indicator.
  - Purchase Ledger selector (Purchase Accounts, Direct Expenses, Indirect Expenses) with live balance indicator.
  - Line items DataGridView: Item / Expense Description, Quantity, Rate (₹), Gross Amount (₹), Discount (₹), Net Amount (₹), and Line Narration.
  - Automatic dynamic line-item recalculation on quantity, rate, and discount changes.
  - Summary panel tracking Subtotal (Gross), Total Discount, and Net Invoice Total.
  - Automatic balanced double-entry generation on save (Purchase A/c Dr Net Total, Supplier Party Cr Net Total).
  - Action buttons and keyboard shortcuts: Save (`Ctrl+A` / `Enter`), Save & New (`Alt+S`), Clear (`Alt+N`), Print (`Ctrl+P`), and Cancel (`Esc`).
- Enhanced `IAccountingService` and `AccountingService`:
  - Added `GetSupplierPartyLedgersAsync`: retrieves supplier accounts belonging to Sundry Creditors, Cash-in-Hand, and Bank Accounts.
  - Added `GetPurchaseLedgersAsync`: retrieves expense and purchase accounts belonging to Purchase Accounts, Direct Expenses, and Indirect Expenses.
- Integrated into `MainForm`:
  - Wired `F9` global shortcut key to launch `PurchaseVoucherForm`.
  - Added toolbar button `F9: Purchase`, menu item `Transactions -> F9 - Purchase`, and Gateway list item `Purchase Voucher (F9)`.
  - Registered `PurchaseVoucherForm` in Dependency Injection in `Program.cs`.
- Added automated unit tests in `Phase13PurchaseTests.cs`:
  - Credit purchase from Sundry Creditor with line items, rate, discount, and creditor balance tracking.
  - Cash purchase of office supplies.
  - Supplier and purchase ledger retrieval filtering.
  - Purchase voucher deletion and dynamic balance restoration.
  - 5/5 new tests passing (73/73 total tests passing across all test suites).


### Added
- Created `SalesVoucherForm`:
  - Full Tally-inspired Section 24 Sales Invoice interface with pure accounting calculations and strictly ZERO GST.
  - Header with dynamic Voucher Number preview (`SLS-00001`), Voucher Date with active FY constraints, Reference / Invoice Number, and Party A/c selector (Sundry Debtors, Cash, Bank) with live balance indicator.
  - Sales Ledger selector (Sales Accounts, Direct Income) with live balance indicator.
  - Line items DataGridView: Item / Service Description, Quantity, Rate (₹), Gross Amount (₹), Discount (₹), Net Amount (₹), and Line Narration.
  - Automatic dynamic line-item recalculation on quantity, rate, and discount changes.
  - Summary panel tracking Subtotal (Gross), Total Discount, and Net Invoice Total.
  - Action buttons and keyboard shortcuts: Save (`Ctrl+A` / `Enter`), Save & New (`Alt+S`), Clear (`Alt+N`), Print (`Ctrl+P`), and Cancel (`Esc`).
- Enhanced `IAccountingService` and `AccountingService`:
  - Added `GetCustomerPartyLedgersAsync`: retrieves customer accounts belonging to Sundry Debtors, Cash-in-Hand, and Bank Accounts.
  - Added `GetSalesLedgersAsync`: retrieves revenue accounts belonging to Sales Accounts, Direct Income, and Indirect Income.
- Integrated into `MainForm`:
  - Wired `F8` global shortcut key to launch `SalesVoucherForm`.
  - Added toolbar button `F8: Sales`, menu item `Transactions -> F8 - Sales`, and Gateway list item `Sales Voucher (F8)`.
  - Registered `SalesVoucherForm` in Dependency Injection in `Program.cs`.
- Added automated unit tests in `Phase12SalesTests.cs`:
  - Credit sales to Sundry Debtor with line items, rate, discount, and balance updates.
  - Cash sales over the counter.
  - Party and sales ledger retrieval filtering.
  - Sales voucher deletion and dynamic balance restoration.
  - 5/5 new tests passing (68/68 total tests passing across all test suites).


### Added
- Created `JournalVoucherForm`:
  - Full Tally-inspired Section 23 & 33 Journal Voucher interface for general adjustments, depreciation, provisions, year-end entries, and non-cash ledger transfers.
  - Pure double-entry grid supporting arbitrary multi-debit and multi-credit compound entries.
  - Intuitive row editing: dual Debit and Credit numeric columns with automatic mutual exclusivity, Dr/Cr indicator, and live ledger balance lookups.
  - Header with dynamic Voucher Number preview (`JRN-00001`), Voucher Date with active FY constraints, and Reference Number.
  - Real-time summary panel tracking Total Debit, Total Credit, and Difference with color-coded balance indicator (`BALANCED` vs `NOT BALANCED`).
  - Action buttons and keyboard shortcuts: Save (`Ctrl+A` / `Enter`), Save & New (`Alt+S`), Clear (`Alt+N`), Print (`Ctrl+P`), and Cancel (`Esc`).
- Integrated into `MainForm`:
  - Wired `F7` global shortcut key to launch `JournalVoucherForm`.
  - Added toolbar button `F7: Journal`, menu item `Transactions -> F7 - Journal`, and Gateway list item `Journal Voucher (F7)`.
  - Registered `JournalVoucherForm` in Dependency Injection in `Program.cs`.
- Added automated unit tests in `Phase11JournalTests.cs`:
  - Year-end depreciation adjustment entries on fixed assets.
  - Compound multi-debit, multi-credit entries (Salaries & Rent expenses with corresponding accrual payables).
  - Strict rejection of unbalanced vouchers.
  - Sequential voucher numbering (`JRN-00001`, `JRN-00002`).
  - Voucher deletion and dynamic balance restoration.
  - 5/5 new tests passing (63/63 total tests passing across all test suites).


### Added
- Created `ContraVoucherForm`:
  - Dedicated Section 22 Contra Voucher interface for internal Cash and Bank fund movements:
    - Cash Deposit to Bank (Bank Dr, Cash Cr)
    - Cash Withdrawal from Bank (Cash Dr, Bank Cr)
    - Inter-Bank Fund Transfers (Destination Bank Dr, Source Bank Cr)
  - Destination Account (Dr) selector strictly populated with Cash and Bank ledgers, with real-time closing balance display.
  - Quick Mode transfer templates ("Cash Deposit to Bank" and "Cash Withdrawal from Bank") for one-click setup.
  - Multi-line credit entries grid with Particulars (Source Cash/Bank A/c), Current Balance lookup, Amount (Cr), Transfer Mode / Instrument Details (Cheque No, NEFT/RTGS/IMPS), and Line Narration.
  - Real-time balance status indicator (`Voucher Balanced (Dr = Cr)` vs error state) and automatic destination Debit computation.
  - Keyboard shortcuts: Save (`Ctrl+A` / `Enter`), Save & New (`Alt+S`), Clear (`Alt+N`), Print (`Ctrl+P`), and Cancel (`Esc`).
- Enhanced `AccountingService`:
  - Added strict validation for `VoucherTypeEnum.Contra`: enforces that every ledger involved in a Contra voucher belongs exclusively to `Cash-in-Hand` or `Bank Accounts` groups (or their sub-groups).
  - Extended `LedgerBalanceDto` with `ClosingBalanceDisplay` and `ClosingBalanceType` compatibility aliases.
- Integrated into `MainForm`:
  - Wired `F4` global shortcut key to launch `ContraVoucherForm`.
  - Added toolbar button `F4: Contra`, menu item `Transactions -> F4 - Contra`, and Gateway list item `Contra Voucher (F4)`.
  - Registered `ContraVoucherForm` in Dependency Injection in `Program.cs`.
- Added automated unit tests in `Phase10ContraTests.cs`:
  - Cash deposit to bank increasing bank balance and decreasing cash balance.
  - Cash withdrawal from bank increasing cash balance and decreasing bank balance.
  - Inter-bank fund transfer between multiple bank accounts.
  - Rejection of non-cash/bank ledgers in Contra vouchers.
  - Voucher deletion restoring both account balances.
  - 5/5 new tests passing (58/58 total tests passing across all test suites).


### Added
- Created `ReceiptVoucherForm`:
  - Full Tally-inspired Receipt Voucher interface (Section 21) with Voucher Number preview (`RCT-00001`), Voucher Date, and Receiving Account dropdown (Cash-in-Hand / Bank Accounts).
  - Real-time dynamic balance lookup for the receiving account, updated immediately upon account selection.
  - Multi-line credit entries DataGridView with Particulars (payer/income ledger selector), Current Balance display, Amount (Cr), and Line Narration.
  - Real-time voucher totals calculation and balance indicator displaying balanced status (`Total Cr == Dr`) and auto-computed receiving Debit.
  - Full keyboard shortcuts: Save (`Ctrl+A` / `Enter`), Save & New (`Alt+S`), Clear (`Alt+N`), Print (`Ctrl+P`), and Cancel (`Esc`).
- Integrated into `MainForm`:
  - Wired `F6` global shortcut key to launch `ReceiptVoucherForm`.
  - Added toolbar button `F6: Receipt`, menu item `Transactions -> F6 - Receipt`, and Gateway of Accounting list entry.
  - Registered `ReceiptVoucherForm` in Dependency Injection in `Program.cs`.
- Added unit test suite in `Phase9ReceiptTests.cs`:
  - Receipt voucher double-entry validation (Dr = Cr).
  - Cash receipt crediting debtor ledger and debiting cash.
  - Bank receipt crediting sales/direct income ledger and debiting bank.
  - Rejection of invalid vouchers (zero amount, missing payer).
  - 4/4 new tests passing (53/53 total tests passing across all test suites).


### Added
- Created `PaymentVoucherForm`:
  - Full Tally-inspired Payment Voucher interface (Section 20) with Voucher Number preview (`PAY-00001`), Voucher Date, and Source Account dropdown (Cash-in-Hand / Bank Accounts).
  - Dynamic display of selected source account's real-time closing balance.
  - Multi-line debit entries DataGridView with Particulars (ledger selector), Current Balance display, Amount (Dr), and Line Narration.
  - Real-time balance calculations and visual balance indicator (`Voucher Balanced (Dr = Cr)` vs error state).
  - Keyboard shortcuts: Save (`Ctrl+A` / `Enter`), Save & New (`Alt+S`), Clear (`Alt+N`), Print (`Ctrl+P`), and Cancel (`Esc`).
- Extended `IAccountingService` and `AccountingService`:
  - `GetCashAndBankLedgersAsync`: retrieves source accounts belonging to Cash and Bank groups.
  - `GetNextVoucherNumberPreviewAsync`: queries next sequential voucher number.
  - `GetVoucherTypeByEnumAsync`: finds or initializes voucher types.
  - `GetVouchersByTypeAsync`, `GetVoucherByIdAsync`, and `DeleteVoucherAsync` (with automatic ledger balance restoration upon soft-delete).
- Integrated into `MainForm`:
  - Wired `F5` global shortcut key to launch `PaymentVoucherForm`.
  - Added toolbar button `F5: Payment`, menu item `Transactions -> F5 - Payment`, and Gateway list entry.
  - Registered `PaymentVoucherForm` in Dependency Injection in `Program.cs`.
- Added unit test suite in `Phase8PaymentTests.cs` (5/5 tests passing; 49/49 total across all test suites).

## [Phase 7: Accounting Engine] - 2026-09-10
### Added
- Created core accounting engine DTOs (`VoucherEntryDto`, `VoucherCreateDto`, `VoucherValidationResult`, `LedgerBalanceDto`, `LedgerStatementLineDto`, `LedgerStatementDto`, `TrialBalanceItemDto`, `TrialBalanceDto`).
- Implemented `IAccountingService` and `AccountingService`:
  - **Double-Entry Balance Enforcement (Section 7 & 17)**:
    - Fundamental rule `Total Debit == Total Credit` enforced with zero allowed discrepancy.
    - Rejects unbalanced vouchers, single-entry vouchers, entries with negative amounts, and entries containing both Debit and Credit amounts.
    - Generates descriptive imbalance reports with computed difference (`|Total Debit - Total Credit|`).
  - **Atomic SQL Transaction Pipeline (Section 19)**:
    - Wraps voucher creation in a strict database transaction (`BeginTransactionAsync`) with rollback on any failure.
    - Validates company, financial year, FY date boundaries (`ValidateDateInCurrentFY`), voucher type, and ledger ownership.
    - Automated sequential voucher numbering (`PAY-00001`, `RCT-00001`).
  - **Dynamic Ledger Calculations (Section 8)**:
    - Dynamic calculation of opening, period debit/credit, and closing balances on demand (`GetLedgerBalanceAsync`). Never stores pre-calculated balances in database columns.
    - Chronological running balance and statement generator (`GetLedgerStatementAsync`) with automatic counterpart particulars resolution.
  - **Trial Balance Engine**:
    - Calculates dynamic Trial Balance (`GetTrialBalanceAsync`) reconciling opening, period, and closing Dr/Cr across all active ledgers with zero discrepancy.
- Registered `IAccountingService` and `AccountingService` into Dependency Injection in `Program.cs`.
- Added comprehensive unit test suite in `Phase7AccountingEngineTests.cs` (10/10 tests passing; 44/44 total across all test suites).

## [Phase 6: Ledger Master] - 2026-09-10
### Added
- Created DTOs for Ledger management (`LedgerCreateDto`, `LedgerUpdateDto`, `LedgerSummaryDto`, `LedgerDetailDto`).
- Implemented `ILedgerService` and `LedgerService`:
  - Ledger creation, update, delete, and retrieval with strict company isolation.
  - Opening balance support with explicit Dr / Cr (`₹10,000 Dr` or `₹5,000 Cr`).
  - Validation: non-negative opening balance (`OpeningBalance >= 0`), unique ledger name per company (case-insensitive), group validation.
  - Deletion protection: strictly prevents deleting a ledger if voucher entries are recorded against it to safeguard double-entry audit trail integrity.
  - Search and filter by name or parent group.
- Built WinForms UI:
  - `LedgerCreateEditForm`: Full Tally-inspired dialog with Group selector, Dr/Cr opening balance, mailing details, banking details, and credit controls.
  - `LedgerListForm`: Searchable DataGridView listing all company ledgers, group filter dropdown, opening balance display with Dr/Cr formatting, and keyboard shortcuts (`Alt+C`, `Alt+A` / `Enter`, `Alt+D`, `F5`, `Esc`).
  - Integrated into `MainForm` (`Masters -> Ledgers` and Gateway of Accounting list) and registered `ILedgerService` into DI in `Program.cs`.
- Added unit test suite in `Phase6LedgerTests.cs` (9/9 tests passing; 34/34 total across all test suites).

## [Phase 5: Group Master] - 2026-09-10
### Added
- Created DTOs for Group management (`GroupCreateDto`, `GroupUpdateDto`, `GroupSummaryDto`, `GroupTreeNodeDto`).
- Implemented `IGroupService` and `GroupService`:
  - Hierarchical group tree generation (`GetGroupTreeAsync`) for recursive nested display.
  - Cycle detection (`DetectCycle`) preventing circular reference loops when reparenting groups.
  - Delete protection preventing deletion of groups containing child groups or existing ledgers.
  - Automatic inheritance of `Nature` and `AffectProfitLoss` properties from parent groups.
  - Duplicate group name validation within the same company.
- Built WinForms UI:
  - `GroupCreateEditForm`: Full dialog for creating and editing groups with parent selection dropdown and primary group toggle.
  - `GroupListForm`: Dual-view management interface featuring both an interactive TreeView and tabular DataGridView, real-time search filtering, and keyboard shortcuts (Alt+C, Alt+A, Alt+D, Esc).
  - Integrated into `MainForm` (`Masters -> Groups`) and registered `IGroupService` into Dependency Injection in `Program.cs`.
- Added unit test suite in `Phase5GroupTests.cs` (7/7 tests passing; 25/25 total across all test suites).

## [Phase 4: Financial Year] - 2026-09-10
### Added
- Created `FinancialYearCreateDto` and `FinancialYearSummaryDto`.
- Updated `ICompanyContext` and `CompanyContext` to support active Financial Year switching (`SetActiveFinancialYear`).
- Implemented `IFinancialYearService` and `FinancialYearService`:
  - Date overlap validation preventing conflicting financial years for the same company.
  - Transaction date boundary validation (`ValidateDateInCurrentFY`) enforcing Section 12 rule (*"Do not allow transactions outside the current financial year"*).
  - Ability to lock and close financial years (`CloseFinancialYearAsync`).
- Built WinForms UI:
  - `FinancialYearCreateForm`: Dialog for creating new FY with automated year name computation (`2027-28`).
  - `FinancialYearListForm`: Management dialog to list, select active, and lock financial years.
  - Updated `MainForm` with "Change Financial Year (F2)", toolbar button, and F2 keyboard shortcut.
- Added comprehensive unit tests in `Phase4FinancialYearTests.cs` including Section 54 multi-FY voucher isolation verification (18/18 tests passing).

## [Phase 3: Company Management] - 2026-09-10
### Added
- Created `CompanyCreateDto`, `CompanyUpdateDto`, `CompanySummaryDto`.
- Built `ICompanyContext` and `CompanyContext` for thread-safe active company/FY state management.
- Implemented `ICompanyService` and `CompanyService` with transaction safety, automatic seeding of 17 standard groups (Section 13), initial Financial Year, and optional 11 default ledgers (Section 15).
- Designed and implemented WinForms UI:
  - `CompanyCreateEditForm`: Full company creation and alteration with validation and ledger seeding option.
  - `CompanyListForm`: Interactive company selection, management, and keyboard shortcuts (Enter, Alt+C, Alt+A, Alt+D, Esc).
  - Updated `MainForm` with Gateway of Accounting company integration, dynamic context updates, and F3 shortcut.
- Added comprehensive unit tests in `Phase3CompanyTests.cs` (12/12 passing).

## [Phase 2: Database Foundation] - 2026-09-10
### Added
- Created dedicated `IEntityTypeConfiguration<T>` classes for all 7 primary accounting tables and supporting tables.
- Refactored `AppDbContext` to use `ApplyConfigurationsFromAssembly` and enforce global `decimal(18,2)` precision on monetary fields.
- Implemented `MoneyFlow.Data/Scripts/CreateDatabaseAndTables.sql` with production-ready T-SQL DDL, indexes, constraints (`CHECK (Debit >= 0)`, `CHECK (Credit >= 0)`), and seed data.
- Built Repository and UnitOfWork layer (`IRepository<T>`, `IUnitOfWork`, `ICompanyRepository`, `IFinancialYearRepository`, `IGroupRepository`, `ILedgerRepository`, `IVoucherRepository`).
- Wired all repositories and `IUnitOfWork` into Dependency Injection in `Program.cs`.
- Added automated unit tests in `Phase2DatabaseTests.cs` testing multi-company isolation, hierarchical groups, voucher cascading deletes, and next voucher number generation (7/7 tests passing).
- Created `DATABASE.md` documenting table schemas, indexes, and relationships.

## [Phase 1: Environment & Project Setup] - 2026-09-10
### Added
- Checked Windows environment (.NET 8 SDK 8.0.424, SQL Server Express instances).
- Initialized Git repository and standard .NET `.gitignore`.
- Created solution `MoneyFlow.sln` with 6 projects:
  - `MoneyFlow.Desktop` (WinForms, .NET 8)
  - `MoneyFlow.Core` (Class library, .NET 8)
  - `MoneyFlow.Data` (Class library, .NET 8, EF Core)
  - `MoneyFlow.Services` (Class library, .NET 8)
  - `MoneyFlow.Reports` (Class library, .NET 8)
  - `MoneyFlow.Tests` (xUnit, .NET 8)
- Configured project references and installed NuGet packages:
  - `Microsoft.EntityFrameworkCore.SqlServer` (8.0.x)
  - `Microsoft.EntityFrameworkCore.Design` (8.0.x)
  - `Microsoft.EntityFrameworkCore.Tools` (8.0.x)
  - `Microsoft.Extensions.Hosting` (8.0.x)
  - `Microsoft.Extensions.DependencyInjection` (8.0.x)
  - `Microsoft.Extensions.Configuration.Json` (8.0.x)
  - `FluentAssertions` & `Microsoft.EntityFrameworkCore.InMemory`
- Created domain entities for Companies, FinancialYears, Groups, Ledgers, VoucherTypes, Vouchers, VoucherEntries, Inventory, Users, Roles, AuditLogs, Settings, and BackupHistory.
- Implemented `AppDbContext` with `decimal(18,2)` monetary precision, indexes, and constraints.
- Created `IDatabaseSetupService` and `DatabaseSetupService` with diagnostics support.
- Generated initial EF Core migration `InitialCreate`.
- Created `DatabaseConnectionDialog` with diagnostic testing button.
- Created `MainForm` with Tally-style Gateway of Accounting layout, shortcuts, and context display.
- Implemented Phase 1 unit test suite covering DbContext mappings, seed validation, and double-entry principle check (3/3 passing).
