# Changelog

All notable changes to the MoneyFlow Desktop Accounting application will be documented in this file.

## [Phase 9: Receipt Voucher (F6)] - 2026-09-10
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
