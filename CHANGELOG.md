# Changelog

All notable changes to the MoneyFlow Desktop Accounting application will be documented in this file.

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
