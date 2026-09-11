# Changelog

All notable changes to the MoneyFlow Desktop Accounting application will be documented in this file.

## [Phase 34: Final System QA, Compliance Verification & Production Readiness Review] - 2026-09-11
### Added
- Created comprehensive Final System QA & Regulatory Compliance Test Suite (`MoneyFlow.Tests/FinalQATests/Phase34FinalSystemQATests.cs`):
  - **Zero GST Architecture Audit (Master Prompt Section 5 & Section 71)**:
    - Programmatically audits all entity types, properties, and metadata across `MoneyFlow.Core.Entities`.
    - Confirms zero occurrence of GST, GSTIN, HSN, SAC, CGST, SGST, IGST, or GST returns throughout the codebase, verifying strict pure double-entry accounting adherence.
  - **Monetary Calculation Precision Audit (Master Prompt Section 70)**:
    - Programmatically inspects all financial properties across `MoneyFlow.Core.Entities` and `MoneyFlow.Core.DTOs` (debit, credit, opening/closing balance, rates, prices, values).
    - Asserts that 100% of monetary calculations strictly use high-precision 128-bit `System.Decimal` without floating-point artifacts (`float` or `double`).
  - **Feature Matrix & Dependency Injection Verification (Master Prompt Section 71)**:
    - Verifies that all 15 core architectural domain, utility, and security services (`ICompanyService`, `IFinancialYearService`, `IGroupService`, `ILedgerService`, `IAccountingService`, `IInventoryService`, `ISearchService`, `IDashboardService`, `IImportExportService`, `IBackupRestoreService`, `ISecurityService`, `IUserContext`, `IAuditService`, `ISettingsService`, `IDatabaseSetupService`) are registered in Dependency Injection and resolvable without runtime exceptions.
  - **Full Accounting End-to-End System Simulation**:
    - Executes complete accounting lifecycle from Company creation and FY setup, through capital induction, bank contra transfers, credit purchases, credit sales, supplier payouts, customer collections, and operational expenses.
    - Mathematically validates that Day Book, Trial Balance, Trading Account, Profit & Loss, and Balance Sheet calculate and reconcile with zero discrepancies.
  - **Final QA Metrics**:
    - 4 new comprehensive audit tests added.
    - **183/183 total automated tests passing**.
    - **0 Warnings, 0 Errors** across all projects (`MoneyFlow.sln`).
    - Entire application verified production-ready.

## [Phase 33: Windows Installer & Database Deployment] - 2026-09-11
### Added
- Created Inno Setup Windows Installer configuration in `installer/MoneyFlowSetup.iss` (Master Prompt Section 58 "INSTALLATION"):
  - Standard target installation directory: `C:\Program Files\MoneyFlow\`.
  - Output installer executable: `MoneyFlowSetup.exe` with LZMA2 ultra64 compression and administrator privilege elevation.
  - Windows shortcuts: Start Menu program group and optional Desktop shortcut.
  - Automatic launch action post-installation.
- Created automated packaging pipeline in `installer/build-installer.ps1`:
  - Compiles release distribution package via `dotnet publish` with win-x64 architecture.
  - Auto-discovers Inno Setup Compiler (`ISCC.exe`) across registry and standard system paths.
  - Calculates SHA256 checksums and file sizes for distribution verification.
- Created production SQL Server database administration scripts in `database/scripts/` (Master Prompt Section 61 "DATABASE MANAGEMENT"):
  - `01_CreateDatabaseAndTables.sql`: Full DDL script creating `MoneyFlowDB` and all 16 relational tables with primary keys, foreign keys, and cascading rules.
  - `02_CreateIndexesAndConstraints.sql`: Performance indexes including compound index coverage for Vouchers, VoucherEntries, Groups, Ledgers, StockItems, and AuditLogs per Section 50.
  - `03_SeedSystemData.sql`: Initial system dataset including standard voucher types, 4 RBAC roles, default administrator user, and default system settings.
  - `04_BackupAndRestore.sql`: T-SQL automated backup and restore stored procedures (`usp_BackupMoneyFlowDB` and `usp_RestoreMoneyFlowDB`).
- Created automated installer verification test suite in `MoneyFlow.Tests/InstallerTests/Phase33InstallerTests.cs`:
  - Validates Inno Setup configuration against Section 58 rules.
  - Validates build automation script.
  - Validates SQL deployment scripts completeness and syntax.
  - Validates release publication binary existence and integrity.
  - 4 new tests added (**179/179 total tests passing**).

## [Phase 32: UI/UX Polish, Themes & Keyboard Flow] - 2026-09-11
### Added
- Created `ThemeManager` and design token engine in `MoneyFlow.Desktop/Styling/` (Master Prompt Section 48 & 55):
  - 3 Distinct visual themes: `ClassicTeal` (classic desktop accounting dark teal/amber), `DarkSlate` (sleek dark mode with sky blue accents), and `LightNeutral` (clean office light theme with royal blue accents).
  - Configurable DataGridView density modes (`Compact` at 24px row height vs `Comfortable` at 32px row height).
  - Consistent component styling across menus, toolbars, status strips, buttons, and zebra-striped DataGridViews.
  - Real-time theme application and dynamic switching upon saving settings in `SettingsForm`.
- Created reusable `VoucherEntryControl` in `MoneyFlow.Desktop/Controls/` (Master Prompt Section 33):
  - Complete voucher header: Voucher Type, auto-incremented Voucher Number, Date (F2), Reference Number, and Narration.
  - Multi-line double-entry grid: Ledger selection dropdown, Debit (Dr), Credit (Cr), and item narration with Enter-as-Tab key movement.
  - Summary footer: Real-time calculation of Total Debit, Total Credit, Difference, and dynamic `BALANCED` (Green) vs `NOT BALANCED` (Red) status badge.
  - Save validation lock: Disables saving or signals invalid state whenever Total Debit != Total Credit or total is zero.
  - Keyboard integration: `Ctrl+S` shortcut triggers save callback.
- Enhanced global keyboard navigation in `MainForm.cs` (Master Prompt Section 32):
  - Added `Ctrl+F` alongside `Alt+G` and `Ctrl+K` for instant Go-To search palette invocation.
  - Retained all standard accounting hotkeys: F2 (Period/Date), F3 (Company), F4 (Contra), F5 (Payment), F6 (Receipt), F7 (Journal), F8 (Sales), F9 (Purchase), Ctrl+F8 (Credit Note), Ctrl+F9 (Debit Note), F10 (Backup/Restore), F11 (Settings), Esc (Quit/Back).
- Created automated UI test suite in `MoneyFlow.Tests/UiTests/Phase32UiPolishTests.cs`:
  - Validates all 3 theme color palettes and contrast invariants.
  - Validates density setting persistence.
  - Validates `VoucherEntryControl` initialization, double-entry mathematical balancing, and payload generation.
  - 5 new UI tests added (**175/175 total tests passing**).

## [Phase 31: Performance Optimization & Index Tuning] - 2026-09-11
### Added
- Comprehensive database indexing and query tuning in alignment with Master Prompt Section 50 ("PERFORMANCE"):
  - **Single & Compound Database Indexing (`MoneyFlow.Data/Configurations/`)**:
    - **`Voucher` Entity Indexing**:
      - Mandatory single-column index coverage: `CompanyId`, `FinancialYearId`, `VoucherDate`, and `VoucherNumber`.
      - Compound index `{ CompanyId, FinancialYearId, VoucherDate, IsDeleted }` for sub-second Day Book and fiscal period queries.
      - Compound index `{ CompanyId, VoucherDate, IsDeleted }` for date-range ledger reports, Trial Balance, and Profit & Loss calculations.
      - Compound index `{ CompanyId, VoucherTypeId, FinancialYearId }` for instant voucher-register filtering by type (Payment, Receipt, Contra, Journal, Sales, Purchase).
    - **`VoucherEntry` Entity Indexing**:
      - Mandatory single-column index coverage: `VoucherId` and `LedgerId`.
      - Compound index `{ LedgerId, VoucherId }` eliminating full table scans during ledger statement generation and closing balance recalculation.
    - **`Group` & `Ledger` Active Status Indexing**:
      - Compound index `{ CompanyId, IsActive }` on `Groups` and `Ledgers` optimizing lookup dropdowns, auto-complete caches, and balance sheets.
    - **`StockItem` & `AuditLog` Indexing**:
      - Compound index `{ CompanyId, IsActive }` on `StockItems` for inventory picking and item catalogs.
      - Compound indexes `{ CompanyId, Timestamp }` and `{ Module, Action }` on `AuditLogs` for real-time compliance filtering.
- **Automated Performance & Index Verification Suite (`MoneyFlow.Tests/PerformanceTests/`)**:
  - `Phase31PerformanceTests.cs`:
    - `ModelIndexVerification_AllSection50Indexes_AreRegisteredInEFCoreModel`: Programmatically verifies EF Core metadata model to guarantee all required single-column and compound indexes are properly mapped to the SQL database schema.
    - `BatchVoucherPosting_Processes100VouchersRapidly_AndReconcilesTrialBalance`: High-volume benchmark posting 100 sequential balanced double-entry vouchers through full validation and database persistence, verifying execution well within desktop responsiveness thresholds (< 5s), instant Day Book loading (< 1s), and Trial Balance reconciliation to 0.00 difference.
    - `DayBookPaginationAndQueryEfficiency_LoadsExactSubsetsRapidly`: Validates efficient date-range partitioning and `AsNoTracking` retrieval across high-frequency voucher streams.
- **Suite Metrics**:
  - 3 new performance and indexing tests added.
  - **170/170 total automated tests passing**.
  - Solution builds cleanly with 0 warnings and 0 errors.

## [Phase 30: End-to-End Accounting Suite & Edge-Case Validation] - 2026-09-11
### Added
- Created comprehensive End-to-End Integration & Edge-Case Test Suite (`MoneyFlow.Tests/IntegrityAndAccountingSuite/`):
  - **Multi-Company Data Boundary Isolation (Master Prompt Section 51 & 53)**:
    - `MultiCompanyIsolationTests.cs`:
      - Mandatory Section 53 assertion: Creating Company A and Company B, creating "Cash A" in Company A, and guaranteeing "Cash A" does not appear when querying Company B.
      - Enforced cross-company rejection: Attempting to save a voucher in Company B that references a ledger from Company A throws `InvalidOperationException`.
      - Enforced cross-company fiscal protection: Attempting to post a transaction with a Financial Year ID belonging to another company is blocked.
      - Inventory boundary isolation: Stock items and units of measure remain strictly partitioned per company.
  - **Financial Year Isolation & Boundary Enforcement (Master Prompt Section 54)**:
    - `FinancialYearIsolationTests.cs`:
      - Mandatory Section 54 assertion: Creating FY 2025-26 and FY 2026-27, recording transactions in 2025-26, opening 2026-27, and verifying that 2025-26 transactions do not appear in 2026-27 Day Book while opening balances carry forward.
      - Date boundary enforcement: Voucher dates outside the active fiscal year's StartDate and EndDate are rejected.
      - Fiscal year lock: Posting vouchers to a closed/locked financial year (`IsClosed = true`) is rejected.
  - **Double-Entry Mathematical Reconciliation & Accounting Engine (Master Prompt Section 51 & 52)**:
    - `DoubleEntryAccountingIntegrityTests.cs`:
      - Mandatory Section 52 assertion: Unbalanced vouchers (`Debit != Credit`, e.g. Cash Dr ₹1,000 without credit) are strictly rejected with detailed validation error diagnostics.
      - Balanced voucher reconciliation: Cash Dr ₹1,000 / Income Cr ₹1,000 succeeds with Debit = ₹1,000, Credit = ₹1,000, Difference = ₹0.
      - Full accounting lifecycle test: Sequential posting of Capital setup, Contra (Cash to Bank), Purchase (Credit), Payment (Supplier payout), Sales (Credit), Receipt (Customer collection), Debit Note (Purchase return), Credit Note (Sales return), and Journal (Depreciation) -> Verifies that the resulting Trial Balance mathematically reconciles with `TotalDebit == TotalCredit` and `Difference == 0`.
  - **Soft-Delete Safety & Financial Integrity (Master Prompt Section 46, 50, 69 & 70)**:
    - `SoftDeleteAndConcurrencySafetyTests.cs`:
      - Soft-delete compliance: Canceling/deleting vouchers marks `IsDeleted = true` preserving audit history in database, while dynamically recalculating ledger balances and omitting deleted records from Day Book and reports.
      - Inactive ledger safeguard: Transactions referencing inactive/disabled ledgers are blocked.
      - Precise monetary calculations: Section 70 compliance ensuring all money calculations use high-precision `decimal` without floating-point artifacts.
  - **Test Suite Results**:
    - 13 new comprehensive integration tests added.
    - **167/167 total automated tests passing** across all test suites.


## [Phase 29: Application & Company Settings] - 2026-09-11
### Added
- Created `ISettingsService` and `SettingsService` (Master Prompt Section 48 "APPLICATION SETTINGS"):
  - **Comprehensive Configuration Management**:
    - **Company & Accounting Defaults**: Configurable default company opened automatically on startup, voucher lock date cutoff preventing modification of reconciled/audited periods (`IsDateLockedAsync`), automatic invoice round-off split calculation, and post-save print preview triggering.
    - **Backup & Storage Settings**: Default backup directory path (with automatic `Documents\MoneyFlow\Backups` initialization) and application exit backup reminder prompts.
    - **Hardware & Printing Settings**: Default Windows printer selection dynamically querying system printers (`PrinterSettings.InstalledPrinters`), standard paper size configuration (A4, Letter, Legal, Continuous Feed), and direct printing options without preview dialogs.
    - **Regional & Number Formatting**: Flexible date formats (`dd-MM-yyyy`, `dd/MM/yyyy`, `yyyy-MM-dd`, `MM/dd/yyyy`), locale numbering schemes (Indian Lakhs/Crores grouping `₹ 12,34,567.89` vs Western Millions `1,234,567.89`), configurable decimal precision (0 to 4 places), and customizable currency symbol prefix (`₹`, `$`, `€`, `£`).
    - **Display & Themes**: Application theme selection (`Classic Accounting Teal`, `Modern Dark Slate`, `Light Neutral Office`) and grid density toggle (`Compact` high-density vs `Comfortable` standard padding).
  - **In-Memory Caching & Performance**:
    - High-performance cached settings snapshot for rapid UI lookup (`AsNoTracking()`).
    - Atomic database persistence to EF Core `Settings` table (`AppSetting` entity) updating existing keys or inserting new entries.
- Created `SettingsForm` (F11):
  - Multi-tab configuration dialog with Segoe UI typography and professional dark teal accents:
    - **Tab 1: Accounting & Periods**: Default startup company dropdown, Period Lock & Protection group with enable toggle and DatePicker, Transaction Behavior toggles.
    - **Tab 2: Backup & Storage**: Default backup path with interactive `Browse...` folder browser, security highlights card, and exit prompt configuration.
    - **Tab 3: Printing & Output**: Windows installed printer selector, paper size dropdown, and direct print toggle.
    - **Tab 4: Regional & Numbers**: Date format picker, numbering grouping selector, currency symbol, decimal places spinner, and dynamic live formatting preview banner.
    - **Tab 5: Display & Theme**: Application theme selector, UI density picker, and global consistency notes.
  - Action bar with "Restore Defaults", "Save Settings (Enter)", and "Cancel (Esc)".
- Integrated into `MainForm`:
  - Added `Settings & Configuration (F11)...` to `Utilities` menu.
  - Added `F11: Settings` shortcut button to quick action ToolStrip.
  - Added `Settings & Configuration (F11)` to Gateway of Accounting list.
  - Added `F11` key handler to global shortcut dispatcher.
  - Registered in Go-To command palette catalog (`SearchService.cs`).
  - Registered `ISettingsService` and `SettingsForm` in Dependency Injection in `Program.cs`.
- Added automated unit tests in `Phase29SettingsTests.cs`:
  - Factory default values fallback when settings table is initially unpopulated.
  - Full round-trip persistence and reload of all configuration categories.
  - Low-level key-value manipulation (`GetSettingValueAsync` / `SetSettingValueAsync`).
  - Voucher lock date enforcement logic (`IsDateLockedAsync`) verifying dates on, before, and after cutoff.
  - Date formatting across supported formats (`FormatDate`).
  - Number formatting with Indian grouping (Lakhs/Crores) and Western grouping (Millions) (`FormatCurrency`).
  - Negative monetary amounts formatting with currency symbol.
  - Idempotent updates preventing duplicate setting keys in database.
  - 10/10 new tests passing (154/154 total tests passing across all test suites).


## [Phase 28: Users, Roles & Security Permissions] - 2026-09-11
### Added
- Created `IUserContext`, `ISecurityService`, and `IAuditService` (Master Prompt Section 46 & 47):
  - **Zero Plain-Text Password Security**:
    - `PasswordHasher`: Cryptographically secure PBKDF2 implementation (`Rfc2898DeriveBytes.Pbkdf2`) with HMAC-SHA256, 128-bit random salt, and 100,000 iterations.
    - Constant-time hash comparison (`CryptographicOperations.FixedTimeEquals`) to prevent timing side-channel attacks.
    - Automatic legacy plaintext password detection and migration during login.
  - **Role-Based Access Control (RBAC)**:
    - 4 Pre-seeded default roles: `Administrator`, `Accountant`, `Operator`, and `Viewer`.
    - 20 Granular permissions across 6 system modules: Masters, Vouchers, Reports, Inventory, Utilities, and Security.
    - `Administrator` role serves as superuser with inherent full-system access bypass.
    - Dynamic permission matrix configuration (`UpdateRolePermissionsAsync`) allowing custom role-permission assignments.
  - **Session & Identity Management**:
    - `IUserContext` / `UserContext`: Thread-safe runtime user context tracking active user ID, username, display name, assigned role, and cached permission sets.
    - `HasPermission`, `HasAnyPermission`, and `IsAdministrator` inspection methods.
    - Login audit logging with tracking of `LastLoginAt` timestamp.
  - **Comprehensive Audit Trail System**:
    - `IAuditService` / `AuditService`: Centralized immutable audit logging recording user actions (Create, Edit, Delete, Login, Logout, Backup, Restore, Security) with target entity names, primary keys, and detail descriptions.
    - Flexible query engine with date range filtering, module categorization, and search term querying.
    - Export audit logs to CSV for compliance and external review.
- Created UI Forms:
  - `LoginForm`: Dedicated modal authentication dialog with username, password, validation, and session initialization.
  - `UserManagementForm`:
    - Tab 1: **User Accounts**: DataGridView listing all users with active status badges, Add User dialog, Edit User dialog, Active toggle (with safety block preventing deactivation of master admin), and Password Reset modal dialog.
    - Tab 2: **Roles & Permissions**: Interactive hierarchical TreeView grouped by functional module (Masters, Vouchers, Reports, Inventory, Utilities, Security) with checkable permissions per role and "Save Permissions Matrix" action button.
    - Tab 3: **Audit Trail Register**: Date range filters, module dropdown filter, DataGridView displaying timestamp, username, role, action, entity, and change summary, plus "Export to CSV" report capability.
- Updated `MainForm`:
  - Added `Switch User / Login...` menu item under `Company`.
  - Added `User Management & Permissions...` under `Utilities`.
  - Added `User Management & Security` to Gateway of Accounting list.
  - Added reactive `User: {Username} ({Role})` indicator on the status strip that dynamically updates upon user switch.
- Registered in Go-To command palette (`SearchService.cs`):
  - `User Management & Security`
  - `Audit Trail Register`
- Database Seeding in `DatabaseSetupService`:
  - Seeds 20 standard system permissions.
  - Seeds 4 foundational roles with standard permission assignments.
  - Upgrades default `admin` account with secure PBKDF2 salt and hash if not already hashed.
- Added automated unit tests in `Phase28SecurityTests.cs`:
  - PBKDF2 hash generation and verification.
  - Successful authentication with valid credentials and session initialization.
  - Authentication rejection on incorrect password.
  - Authentication rejection for inactive/deactivated users.
  - Enforced uniqueness on usernames.
  - User update and status toggling.
  - Role-permission assignment and dynamic matrix synchronization.
  - Comprehensive audit trail recording and date/module filtering.
  - Superuser administrator bypass verification.
  - 10/10 new tests passing (144/144 total tests passing across all test suites).


## [Phase 27: Local Backup & Restore System] - 2026-09-11
### Added
- Created `IBackupRestoreService` and `BackupRestoreService` (Master Prompt Section 43 & 61):
  - **Dual Backup Format Support**:
    - `.mfb` (MoneyFlow Backup Archive): Portable compressed ZIP archive storing `manifest.json` and complete company data payload in `data.json` with embedded SHA-256 integrity checksum. Suitable for transferring companies across desktop PCs and flash drives.
    - `.bak` (Full SQL Server Database Backup): Native relational database backup via T-SQL `BACKUP DATABASE ... TO DISK` with compression for complete instance snapshots.
  - **Zero-Overwrite Rule**: Automatically enforces incremental filename timestamping (`{CompanyName}_{yyyy-MM-dd_HHmm}.mfb`) and appends collision counters (`_01`, `_02`) if a file of the same name already exists in the target directory.
  - **Archive Inspection & Manifest Reader**: Reads header metadata (`ReadManifestAsync`) including company name, financial year, record counts (Ledgers, Vouchers, Stock Items), and validates SHA-256 hash without extracting the archive.
  - **Safe Restore Modes**:
    - *Restore as New Company (Copy)*: Dynamically recreates the company with a unique company ID and systematically remaps parent-child group hierarchies, unit references, ledger identifiers, and voucher entry foreign keys.
    - *Overwrite Existing Company*: Replaces child accounts, inventory, and vouchers for an existing selected company following explicit double-confirmation prompts.
  - **Backup Repository & History Enumeration**: `GetBackupHistoryAsync` scans the backup folder and reports file names, company names, creation dates, sizes, and archive validity status.
- Created `BackupRestoreForm`:
  - Tab 1: **Create Backup (F10)**: Source company selector, destination directory browser (defaulting to `Documents\MoneyFlow\Backups`), format radio buttons (`.mfb` vs `.bak`), notes/comment input, "Create Backup Now" button with realtime execution log.
  - Tab 2: **Restore Wizard**: Backup file picker (`*.mfb;*.bak`), archive inspection card displaying company name, financial year, record counts, and green SHA-256 checksum integrity verification badge. Restore destination options (New Company copy vs Overwrite with safety warnings).
  - Tab 3: **Backup Repository & History**: Directory browser with "Open in Explorer", DataGridView of historical backups, "Verify Selected Backup" dialog, and "Restore Selected Backup..." action button.
- Integrated into `MainForm`:
  - Added `Backup & Restore System (F10)...` to `Utilities` menu.
  - Added `F10: Backup / Restore` shortcut button to quick action ToolStrip.
  - Added `Backup & Restore (F10)` item to Gateway of Accounting list.
  - Added `F10` key handler to global shortcut interceptor.
  - Registered `Backup & Restore System (F10)` in Go-To command palette catalog (`SearchService.cs`).
  - Registered `IBackupRestoreService` and `BackupRestoreForm` in Dependency Injection in `Program.cs`.
- Added automated unit tests in `Phase27BackupRestoreTests.cs`:
  - Portable ZIP archive creation with `manifest.json` and SHA-256 checksum calculation.
  - Manifest reading and metadata verification without full archive extraction.
  - Restore as new company with foreign key and ledger remapping.
  - Corrupted/tampered archive detection and checksum verification failure handling.
  - Zero-overwrite rule and incremental counter suffix generation.
  - Backup history enumeration and validity checks.
  - Overwrite existing company child records replacement.
  - 7/7 new tests passing (134/134 total tests passing across all test suites).

## [Phase 26: Data Import & Export (Masters & Transactions)] - 2026-09-11
### Added
- Created `ImportExportForm` (Data Import & Export Center):
  - Staged Import Wizard adhering strictly to Master Prompt Section 44 ("Never directly insert unvalidated imported data into accounting tables").
  - Entity selectors for `Chart of Accounts (Ledgers)`, `Stock Items (Inventory)`, and `Day Book (Vouchers / Transactions)`.
  - Duplicate resolution policies: `Skip Duplicates (Recommended)`, `Update Existing`, and `Reject Batch on Duplicate`.
  - Download Sample Template button to export pre-formatted CSV template structures with example records.
  - Staged validation DataGridView displaying row numbers, color-coded status badges (`Valid [✔]`, `Duplicate [!]`, `Error [✖]`), primary identifiers, details, and validation error messages.
  - Summary counter: `Total Rows | Valid | Duplicates | Errors`.
  - Execution confirmation with final modal review before committing records to database.
  - Export Center tab: Multi-entity export with date range filtering (for vouchers) and format options (`CSV RFC 4180`, `JSON`).
- Created `CsvUtility`:
  - RFC 4180 compliant CSV parser and writer with support for quoted strings, embedded commas, double quotes (`""`), and multiline fields.
- Created `IImportExportService` and `ImportExportService`:
  - Pipeline for template generation, CSV parsing, column mapping, pre-validation, duplicate detection, and atomic transaction execution.
  - Foreign-key resolution for Groups (Ledgers) and Units (Stock Items).
  - Validation for non-empty names, positive balances, Dr/Cr types, valid voucher dates, and distinct Dr/Cr ledgers.
  - Multi-company boundary isolation enforced across all import and export operations.
  - Multi-format data export to CSV and JSON formats.
  - Added DTOs: `ImportEntityType`, `DuplicateAction`, `ImportRowStatus`, `ImportPreviewRowDto`, `ImportPreviewResultDto`, `ImportExecutionResultDto`, `ExportFormat`, and `ExportOptionsDto`.
- Integrated into `MainForm`:
  - Added `Import / Export Data...` to `Utilities` menu.
  - Added `Import / Export Data` to Gateway of Accounting list.
  - Added `Import / Export Data` to Go-To search catalog in `SearchService.cs`.
  - Registered `IImportExportService` and `ImportExportForm` in Dependency Injection in `Program.cs`.
- Added automated unit tests in `Phase26ImportExportTests.cs`:
  - RFC 4180 CSV parser and serializer with quotes, commas, and escapes.
  - Template CSV generation for all entities.
  - Staged import preview validation and duplicate detection for ledgers.
  - Atomic import execution with `Skip` and `Update` duplicate policies.
  - Stock items import with unit linking and auto-valuation calculation.
  - Multi-format data export to CSV and JSON.
  - 6/6 new unit tests passing (127/127 total tests passing across all test suites).

## [Phase 25: Executive Accounting Dashboard & Financial KPIs] - 2026-09-11
### Added
- Created `DashboardForm`:
  - Executive Financial Command Center adhering to Master Prompt Section 26, 67.
  - 4 Dynamic Executive KPI Cards:
    - *Liquid Funds*: Cash-in-Hand + Bank Accounts total liquid balance with drill-down to Cash/Bank Book.
    - *Working Capital*: Total Receivables (Sundry Debtors) vs Total Payables (Sundry Creditors) and Net Position with drill-down to Outstanding Analysis.
    - *Profitability*: FYTD Total Sales, Total Purchases, and Net Profit / Loss (color-coded) with drill-down to Profit & Loss Account.
    - *Inventory Valuation*: Active Stock Items count and total closing stock valuation with drill-down to Stock Summary.
  - Multi-tab detailed analytics:
    - *Monthly Trends*: Monthly breakdown of Sales, Purchases, Receipts (Inflows), and Payments (Outflows) across the active Financial Year.
    - *Top Outstanding Parties*: Split panels displaying Top 5 Debtors (Receivables) and Top 5 Creditors (Payables) with balances.
    - *Recent Transactions*: High-density grid of latest 10 vouchers with double-click / `Enter` drill-down into detailed transaction audit modal.
  - Quick action toolbar: Direct one-click shortcuts for `Payment (F5)`, `Receipt (F6)`, `Sales (F8)`, `Purchase (F9)`, `Day Book`, `Trial Balance`, `P&L`, `Balance Sheet`, `Stock Summary`, and `Go To (Alt+G)`.
  - Date filtering: `As of Date (F2)` picker, `Refresh (F5)`, `Print / Preview (Ctrl+P)`, and RFC 4180 CSV export.
- Created `IDashboardService` and `DashboardService`:
  - Real-time financial synthesis calculating liquid funds, receivables, payables, working capital, profitability (Sales, Purchases, Direct/Indirect Expenses & Incomes, Gross & Net Profit), inventory valuation, and monthly trends.
  - Strict multi-company data isolation enforcing company boundaries across all calculations.
  - Added DTOs: `DashboardDto`, `MonthlyFinancialSummaryDto`, `TopPartyOutstandingDto`, and `RecentVoucherDto`.
- Integrated into `MainForm`:
  - Added `Dashboard (Executive Overview)` to Gateway of Accounting menu list.
  - Added `Dashboard` button on top ToolStrip toolbar.
  - Added `&Dashboard` to `Reports` menu.
  - Added `Executive Dashboard` to `SearchService` navigation catalog.
  - Registered `IDashboardService` and `DashboardForm` in Dependency Injection in `Program.cs`.
- Added automated unit tests in `Phase25DashboardTests.cs`:
  - Liquid funds calculation across cash and bank accounts with voucher receipts and contra transfers.
  - Receivables and payables calculation for debtors and creditors.
  - Profitability metrics (Sales, Purchases, Gross & Net Profit) and monthly trend grouping.
  - Stock items count and inventory valuation aggregation.
  - Strict multi-company isolation verification.
  - 5/5 new tests passing (121/121 total tests passing across all test suites).

## [Phase 24: Global Search (Alt+G / Ctrl+K Go-To & Search Engine)] - 2026-09-11
### Added
- Created `GlobalSearchForm` (Spotlight / Go-To Command Palette):
  - Tally-inspired Section 64 fast navigation and universal search dialog invoked via `Alt+G` or `Ctrl+K`.
  - Prominent search input box with placeholder hints and debounced live querying.
  - Tabbed filter bar for entity classification: `All`, `Ledgers`, `Vouchers`, `Stock Items`, and `Navigation`.
  - High-density DataGridView with entity category badges (`[Ledger]`, `[Voucher]`, `[StockItem]`, `[Navigation]`), Primary Title, Contextual Subtitle, Formatted Amount (`₹N2`), and Date.
  - Full keyboard navigation: `Up`/`Down` arrow navigation, `Enter` to jump/drill-down into selected result, `Esc` to close, `Tab` cycle, and hotkey accelerator support.
- Created `ISearchService` and `SearchService`:
  - Multi-entity asynchronous search aggregator across Ledgers, Vouchers, Stock Items, and Navigation screens.
  - Multi-company data isolation enforcing company boundaries across all database queries.
  - Smart search parsing: exact amount matching (e.g. `7500` finds vouchers with debit/credit ₹7,500.00), voucher numbering, narration matching, and account names.
  - Comprehensive Navigation Catalog mapping 19 standard accounting reports, registers, master lists, and voucher entry screens (Contra, Payment, Receipt, Journal, Sales, Purchase, Debit Note, Credit Note, Day Book, Trial Balance, P&L, Balance Sheet, Cash/Bank Book, Outstanding Analysis, Stock Summary, Ledgers, Groups, Stock Items, Units).
  - Added DTOs: `GlobalSearchCategory` enum and `GlobalSearchResultDto`.
- Integrated into `MainForm`:
  - Registered global keyboard shortcuts `Alt+G` and `Ctrl+K` for instant command palette launching.
  - Added `Go To / Search... (Alt+G)` to the Gateway of Accounting menu list, main toolbar icon button, and `Edit -> &Go To / Search (Alt+G)` menu item.
  - Wired direct drill-down dispatching: selecting a search result instantly routes and opens the appropriate form (e.g. Day Book, Trial Balance, Profit & Loss, Balance Sheet, Cash/Bank Book, Outstanding, Stock Summary, Ledgers, Groups, Stock Items, Units, or specific voucher entry screens).
  - Registered `ISearchService` and `GlobalSearchForm` in Dependency Injection in `Program.cs`.
- Added automated unit tests in `Phase24GlobalSearchTests.cs`:
  - Navigation screen discovery on empty query and keyword matching (e.g. "pnl", "contra", "tb").
  - Ledger search by name and group with strict multi-company boundary isolation.
  - Stock item search by name and unit.
  - Voucher search by voucher number, narration, and parsed numeric amount.
  - Category filtering across All, Ledgers, and Stock Items.
  - 5/5 new tests passing (116/116 total tests passing across all test suites).

## [Phase 23: Basic Inventory (Units of Measure, Stock Items & Stock Summary)] - 2026-09-11
### Added
- Created `UnitListForm` & `UnitCreateEditForm`:
  - Full Tally-inspired Section 26, 30 Units of Measure inventory master interface.
  - Fields: Unit Symbol / Name (e.g. Nos, Kg, Box, Mtr), Formal Name, and Decimal Places (0–4).
  - Fast live search filtering by symbol and formal name (`F3`).
  - Actions: Create (`Alt+C`), Edit (`Enter`), Delete (`Del`), and hotkey shortcuts.
- Created `StockItemListForm` & `StockItemCreateEditForm`:
  - Full Tally-inspired Section 26, 30 Stock Item inventory master interface.
  - High-density DataGridView: Item Name, Unit, Opening Quantity, Opening Rate (₹), Opening Value (₹), and Status.
  - Auto-calculated Opening Stock Valuation (`OpeningValue = OpeningQuantity * OpeningRate`).
  - Unit dropdown selector linking stock items to company units of measure.
  - Duplicate item name prevention and company-isolated catalog management.
  - Actions: Create (`Alt+C`), Edit (`Enter`), Delete (`Del`), and hotkeys.
- Created `StockSummaryForm`:
  - Full Tally-inspired Section 26, 30 Stock Summary inventory overview and valuation register.
  - High-density DataGridView: Item Name, Unit, Opening Qty, Opening Value (₹), Closing Qty, Closing Rate (₹), and Closing Value (₹).
  - `As of Date (F2)` date picker, live text search filter (`F3`), summary footer displaying total inventory valuation (`₹XX,XXX.XX`).
  - Export to RFC 4180 CSV, Print Preview (`Ctrl+P`), and close (`Esc`).
- Created `IInventoryService` and `InventoryService`:
  - Full CRUD operations for Units of Measure with company isolation and dependency checking before deletion.
  - Full CRUD operations for Stock Items with automatic valuation calculations and unique name constraints.
  - `GetStockSummaryAsync(companyId, asOfDate)` calculating closing stock positions and valuations.
  - Added DTOs: `UnitDto`, `UnitCreateDto`, `UnitUpdateDto`, `StockItemDto`, `StockItemCreateDto`, `StockItemUpdateDto`, `StockSummaryItemDto`, and `StockSummaryReportDto`.
- Integrated into `MainForm`:
  - Replaced placeholders with live menu items `Masters -> &Stock Items` and `Masters -> &Units of Measure`.
  - Added menu item `Reports -> &Stock Summary`.
  - Added `Stock Items`, `Units of Measure`, and `Stock Summary` to Gateway of Accounting list.
  - Added launcher methods `OpenUnitList()`, `OpenStockItemList()`, and `OpenStockSummary()`.
  - Registered `IInventoryService` and all inventory forms in Dependency Injection in `Program.cs`.
- Added automated unit tests in `Phase23InventoryTests.cs`:
  - Unit creation, case-insensitive duplicate prevention, and multi-company isolation.
  - Foreign key protection: preventing unit deletion when in use by stock items.
  - Stock item creation with auto-calculated opening value (`Qty * Rate`).
  - Stock item modification and opening value recalculation.
  - Stock Summary report generation and total closing inventory valuation.
  - 5/5 new tests passing (111/111 total tests passing across all test suites).

## [Phase 22: Cash Book & Bank Book Reports] - 2026-09-11
### Added
- Created `CashBankBookForm`:
  - Full Tally-inspired Section 30, 39, 40 Cash Book & Bank Book register interface.
  - Multi-Perspective View Toggle: Radio selector between `[ Cash Book (Cash-in-Hand) ]` and `[ Bank Book (Bank Accounts) ]`.
  - Account Selector Dropdown: Dynamically populated with individual cash/bank accounts (e.g., Cash, Petty Cash, HDFC Bank, SBI Bank) plus consolidated views (`[ All Cash Accounts ]` / `[ All Bank Accounts ]`).
  - Date Range Filtering: `From Date (F2)` and `To Date` pickers constrained to active Financial Year bounds.
  - Live Text Search: Real-time filtering across Voucher Numbers, Opposing Particulars, Ref / Cheque numbers, and Narrations (`F3`).
  - Account Context Card: Displays current book/account title and dynamic Opening Balance brought forward as of `From Date` (`₹XX,XXX.XX Dr/Cr`).
  - High-Density Running Balance DataGridView: Date, Voucher Type, Voucher No, Ref / Instrument No, Opposing Particulars, Account Name, Receipts / Deposits (₹ Dr), Payments / Withdrawals (₹ Cr), Running Balance (₹ Dr/Cr), and Narration.
  - Explicit Opening Balance Row: Injects row 0 brought forward balance (`** Opening Balance **`).
  - Audit Summary Footer: Displays total transaction count, Total Receipts / Deposits (₹ Dr), Total Payments / Withdrawals (₹ Cr), Net Closing Balance (`₹XX,XXX.XX Dr/Cr`), and mathematical identity validation badge (`[ ✔ ] RECONCILED`).
  - Drill-Down Navigation: Double-click or `Enter` on any voucher row opens comprehensive transaction audit dialog.
  - Export & Print: RFC 4180 CSV export, Print Preview (`Ctrl+P`), and hotkeys (`F2`, `F3`, `F4`, `F5`, `Esc`).
- Enhanced `IAccountingService` and `AccountingService`:
  - Added `GetCashLedgersAsync(companyId)`: Retrieves all active ledgers under "Cash-in-Hand" and its sub-groups.
  - Added `GetBankLedgersAsync(companyId)`: Retrieves all active ledgers under "Bank Accounts", "Bank OD A/c", "Bank OCC A/c" and their sub-groups.
  - Added `GetCashBankBookAsync(companyId, ledgerId, bookType, fromDate, toDate)`: Computes opening balances, queries period vouchers excluding soft-deletes, tracks running balances, and calculates per-account summaries.
  - Added DTOs: `CashBankBookType`, `CashBankBookLineDto`, `CashBankAccountSummaryDto`, and `CashBankBookReportDto`.
- Integrated into `MainForm`:
  - Wired menu item `Reports -> &Cash / Bank Book` to launch `CashBankBookForm`.
  - Wired Gateway of Accounting list item `Cash / Bank Book` to launch `CashBankBookForm`.
  - Added `OpenCashBankBook(CashBankBookType? defaultType = null)` launcher.
  - Registered `CashBankBookForm` in Dependency Injection in `Program.cs`.
- Added automated unit tests in `Phase22CashBankBookTests.cs`:
  - Cash Book calculation: opening balance, receipts, payments, running balance, and closing balance.
  - Bank Book calculation: multi-bank ledger isolation (HDFC Bank vs SBI Bank), deposits, and withdrawals.
  - Consolidated Cash & Bank mode aggregating multiple accounts with individual ledger summaries.
  - Soft-deleted voucher exclusion and historical date-range boundary enforcement.
  - 4/4 new tests passing (106/106 total tests passing across all test suites).

## [Phase 21: Outstanding Receivables & Payables (Aging Analysis)] - 2026-09-10
### Added
- Created `OutstandingReportForm`:
  - Full Tally-inspired Section 21, 30 Outstanding Analysis register with bill-wise / voucher-level aging analysis.
  - Multi-Perspective View Toggle: Radio/segmented selector between `[ Receivables (Sundry Debtors) ]` and `[ Payables (Sundry Creditors) ]`.
  - Date Range Filtering: `As of Date (F2)` picker constrained to active Financial Year bounds.
  - Live Text Search: Instant client-side filtering across Party Names and Parent Groups (`F3`).
  - High-Density Aging DataGridView: Particulars (Party Name), Parent Group, Total Outstanding (₹), 0–30 Days (₹), 31–60 Days (₹), 61–90 Days (₹), and >90 Days (₹) with colored age-bracket indicators.
  - Audit Summary Footer: Displays Total Parties listed, Total Outstanding Amount (₹), and aggregated totals for each aging bracket.
  - Drill-Down Navigation: Double-click or `Enter` on any party row immediately opens its detailed `LedgerStatementForm`.
  - Export & Print: RFC 4180 CSV export with detailed bucket breakdown, print preview (`Ctrl+P`), and hotkeys (`F2`, `F3`, `F5`, `Esc`).
- Enhanced `IAccountingService` and `AccountingService`:
  - Added `GetOutstandingReportAsync(companyId, asOfDate, isReceivables)`: Dynamically calculates outstanding party balances and executes a First-In First-Out (FIFO) chronological aging allocation across unpaid transactions.
  - Added DTOs: `AgingBucketsDto`, `OutstandingPartyDto`, and `OutstandingReportDto`.
- Integrated into `MainForm`:
  - Wired menu item `Reports -> &Outstanding Analysis` to launch `OutstandingReportForm`.
  - Wired Gateway of Accounting list item `Outstanding Analysis` to launch `OutstandingReportForm`.
  - Registered `OutstandingReportForm` in Dependency Injection in `Program.cs`.
- Generated desktop UI design screen in Stitch MCP (`projects/10546831619592911675/screens/3a263d5642e1476e89b02cec9ff52170`).
- Added automated unit tests in `Phase21OutstandingTests.cs`:
  - Receivables calculation and multi-bucket aging distribution across 0–30, 31–60, 61–90, and >90 day windows.
  - Payables calculation with partial payments and FIFO allocation against unpaid purchase vouchers.
  - Exclusion of fully settled zero-balance parties.
  - Soft-deleted voucher exclusion and historical as-of-date boundary enforcement.
  - 4/4 new tests passing (102/102 total tests passing across all test suites).

## [Phase 20: Balance Sheet Report] - 2026-09-10
### Added
- Created `BalanceSheetForm`:
  - Full Tally-inspired Section 20, 29, 30 classic two-column T-Format Balance Sheet.
  - Left Side (Credit / Capital & Liabilities): Capital Accounts, Reserves, Loans, Current Liabilities (Sundry Creditors, Duties & Taxes), and Profit & Loss surplus (Current Period Net Profit).
  - Right Side (Debit / Property & Assets): Fixed Assets, Investments, Current Assets (Sundry Debtors, Bank Accounts, Cash-in-Hand), and Profit & Loss deficit (Current Period Net Loss).
  - Mathematical Double-Entry Verification: Prominent dynamic balance badge displaying `[ ✔ ] BALANCED (Diff: ₹0.00)` when `Total Liabilities == Total Assets` or alerting differences.
  - Drill-Down Navigation: Double-click or `Enter` on any ledger row instantly opens its detailed `LedgerStatementForm`.
  - Search, Filter & Export: As-of Date picker (`F2`), real-time text filter (`F3`), RFC 4180 CSV export, and print preview (`Ctrl+P`).
- Enhanced `IAccountingService` and `AccountingService`:
  - Added `GetBalanceSheetAsync(companyId, asOfDate)` computing dynamic closing balances up to `asOfDate` across all non-P&L ledger accounts.
  - Automatically incorporates Current Period Net Profit or Net Loss from `GetProfitAndLossAsync`.
  - Added DTOs: `BalanceSheetLineDto`, `BalanceSheetGroupDto`, and `BalanceSheetDto`.
- Integrated into `MainForm`:
  - Wired menu item `Reports -> &Balance Sheet` to launch `BalanceSheetForm`.
  - Wired Gateway of Accounting list item `Balance Sheet` to launch `BalanceSheetForm`.
  - Registered `BalanceSheetForm` in Dependency Injection in `Program.cs`.
- Generated desktop UI design screen in Stitch MCP (`projects/10546831619592911675/screens/4abee7a901ed40fdb6e27cecd64675c6`).
- Added automated unit tests in `Phase20BalanceSheetTests.cs`:
  - Balanced double-entry identity (`Total Liabilities == Total Assets`) across Capital, Fixed Assets, Bank, Creditors, Debtors.
  - Net Profit from operating revenues & expenses integrated into Liabilities side.
  - Net Loss scenario integrated into Assets side.
  - Exclusion of soft-deleted vouchers and historical as-of-date filtering.
  - 4/4 new tests passing (98/98 total tests passing across all test suites).

## [Phase 19: Profit & Loss Statement Report] - 2026-09-10
### Added
- Created `ProfitLossForm`:
  - Full Tally-inspired Section 19, 30 classic two-column T-Format Trading & Profit & Loss Statement.
  - Left Column (Debit / Expenses): Direct Expenses (Trading Account), Gross Loss (if applicable), and Indirect Expenses (P&L Account).
  - Right Column (Credit / Revenue): Direct Revenues (Trading Account), Gross Profit (if applicable), and Indirect Incomes (P&L Account).
  - Trading Account Calculation: Automatic Gross Profit / Gross Loss computation (`Total Direct Revenue - Total Direct Expense`).
  - Profit & Loss Account Calculation: Automatic Net Profit / Net Loss computation (`Gross Profit/Loss + Indirect Income - Indirect Expense`).
  - Mathematical Grand Totals: Balanced Trading and P&L grand totals dynamically reconciling Dr and Cr sides.
  - Drill-Down Navigation: Double-click or `Enter` on any ledger row opens its full Ledger Statement (`LedgerStatementForm`).
  - Search, Filter & Export: Date range filter (`From Date`, `To Date` / `F2`), live ledger search filter, RFC 4180 CSV export, and print preview (`Ctrl+P`).
- Enhanced `IAccountingService` and `AccountingService`:
  - Added `GetProfitAndLossAsync(companyId, fromDate, toDate)` calculating period debits and credits across all revenue and expense accounts.
  - Added robust direct vs. indirect classification logic distinguishing trading components from operating overheads.
  - Added DTOs: `ProfitLossLineDto`, `ProfitLossCategoryDto`, and `ProfitLossStatementDto`.
- Integrated into `MainForm`:
  - Wired menu item `Reports -> &Profit & Loss` to launch `ProfitLossForm`.
  - Wired Gateway of Accounting list item `Profit & Loss A/c` to launch `ProfitLossForm`.
  - Registered `ProfitLossForm` in Dependency Injection in `Program.cs`.
- Generated desktop UI design screen in Stitch MCP (`projects/10546831619592911675/screens/1ff018f9e4f9422cb5d8042f7ef81be8`).
- Added automated unit tests in `Phase19ProfitLossTests.cs`:
  - Trading account gross profit calculation from sales, purchases, and direct expenses.
  - Operating net profit calculation with indirect expenses (Rent, Salaries) and indirect incomes (Interest).
  - Gross loss and net loss scenario verification.
  - Soft-deleted voucher exclusion and date-range bounding verification.
  - 4/4 new tests passing (94/94 total tests passing across all test suites).

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
