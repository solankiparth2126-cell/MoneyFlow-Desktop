# MoneyFlow Desktop ERP — Standalone Windows Accounting Application

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![Windows Forms](https://img.shields.io/badge/UI-Windows%20Forms-green.svg)]()
[![Database](https://img.shields.io/badge/Database-SQL%20Server%20Express-red.svg)]()
[![Tests](https://img.shields.io/badge/Tests-183%20Passed-brightgreen.svg)]()
[![Architecture](https://img.shields.io/badge/Accounting-Zero%20GST%20%7C%20Pure%20Double--Entry-purple.svg)]()

**MoneyFlow Desktop ERP** is a high-performance, standalone Windows desktop accounting software designed from the ground up for micro, small, and medium businesses. Inspired by the workflow efficiency, keyboard-first usability, and speed of applications such as TallyPrime, MoneyFlow is a completely independent, modern C#/.NET 8 implementation.

---

## 🌟 Core Architectural Principles

1. **Strict Double-Entry Accounting**:
   - Every transaction enforces $\text{Total Debit} = \text{Total Credit}$. Unbalanced vouchers are rejected by the accounting engine before touching the database.
2. **Strict Zero GST (100% Pure Accounting)**:
   - In strict adherence to design specifications, the software contains zero GST, GSTIN, HSN, SAC, CGST, SGST, IGST, or tax return overhead. Pure commercial accounting only.
3. **128-Bit Decimal Precision**:
   - All monetary calculations use `System.Decimal` (SQL `decimal(18,2)` / `decimal(18,4)`), guaranteeing zero IEEE-754 binary floating-point roundoff errors.
4. **Offline & Standalone Operation**:
   - Designed to run completely offline on a single Windows PC with a local SQL Server Express instance. No cloud lock-in, no web server requirements, no external network dependencies.
5. **Multi-Company & Fiscal Boundary Isolation**:
   - Strict `CompanyId` and `FinancialYearId` partitioning prevents cross-company data leakage and cross-period contamination while preserving historical closing balances.
6. **Keyboard-First Ergonomics**:
   - Seamless data entry designed for rapid numeric keypad and keyboard operation with minimal mouse dependency.

---

## 📂 Solution Structure

```text
MoneyFlow/
├── MoneyFlow.Desktop/           # WinForms presentation layer, keyboard dispatchers, Gateway UI
│   ├── Controls/                # Reusable controls (VoucherEntryControl)
│   ├── Dialogs/                 # Diagnostics & Database connection dialogs
│   ├── Forms/                   # 36 dedicated accounting forms & registers
│   └── Styling/                 # ThemeManager & design tokens (ClassicTeal, DarkSlate, LightNeutral)
├── MoneyFlow.Core/              # Domain entities, DTOs, Enums, Interfaces, Validation
├── MoneyFlow.Data/              # EF Core AppDbContext, Fluent API mappings, compound indexes
├── MoneyFlow.Services/          # 15 Domain services (Accounting, Company, Ledger, Backup, Security, etc.)
├── MoneyFlow.Reports/           # RDLC report definitions, printing engine, PDF/Excel generators
├── MoneyFlow.Tests/             # 183 automated unit, integration, and compliance tests
├── database/scripts/            # Standalone T-SQL administrative DDL, indexes, and seed scripts
└── installer/                   # Inno Setup compiler script (MoneyFlowSetup.iss) & build pipeline
```

---

## ⚡ Feature Matrix

### 1. Masters & Chart of Accounts
- **Company Management**: Create, Alter, Switch, Close, and Delete companies. Supports custom mailing details, fiscal years, and books beginning dates.
- **Financial Year Engine**: Independent fiscal years (`2025-26`, `2026-27`, etc.) with date range validation, closing balance carry-forward, and fiscal locking (`IsClosed`).
- **Group Hierarchy**: Nested multi-level chart of accounts with 28 standard Indian accounting groups (Assets, Liabilities, Income, Expenses).
- **Ledgers**: Complete ledger accounts with opening balances, Dr/Cr types, contact records, active toggles, and live closing balance tracking.
- **Basic Inventory**: Item master with Units of Measure (UOM), opening stock quantities, valuation rates, and closing stock calculation.

### 2. Double-Entry Voucher Suite
| Shortcut | Voucher Type | Usage Description |
| :--- | :--- | :--- |
| **F4** | **Contra** | Internal cash-to-bank, bank-to-cash, or inter-bank account transfers |
| **F5** | **Payment** | Outgoing funds for vendor settlements and operational expenditures |
| **F6** | **Receipt** | Incoming capital, customer collections, and miscellaneous income |
| **F7** | **Journal** | Year-end adjustments, accruals, depreciation, and ledger-to-ledger entries |
| **F8** | **Sales** | Commercial sales invoicing with customer ledger and item details |
| **F9** | **Purchase** | Raw material and inventory inward billing against vendor accounts |
| **Ctrl+F8** | **Credit Note** | Sales returns and outward customer credit adjustments |
| **Ctrl+F9** | **Debit Note** | Purchase returns and vendor debit adjustments |

### 3. Financial Reports & Analytics
- **Day Book**: Filterable transaction log by date range, voucher type, and company with instant drill-down.
- **Ledger Statement (Account Book)**: Chronological account ledger with running balance calculation.
- **Trial Balance**: Multi-column worksheet verifying opening, period, and closing debits and credits with zero difference reconciliation.
- **Trading & Profit & Loss Statement**: Gross profit from trading revenue/expenses and net period profit.
- **Balance Sheet**: Traditional T-format statement of financial position (Capital & Liabilities vs Assets) incorporating period profit/loss.
- **Cash Book & Bank Book**: Filtered liquid asset registers.
- **Outstanding Receivables & Payables**: Aging analysis partitioned into 0–30, 31–60, 61–90, and 90+ day buckets.
- **Stock Summary**: Inventory quantity, average valuation rate, and total closing valuation.

### 4. Utilities, Security & Settings
- **Global Search & Go-To (Alt+G / Ctrl+K / Ctrl+F)**: Instant fuzzy search across forms, ledgers, vouchers, and stock items.
- **Executive Dashboard**: Real-time financial KPI cards (Cash in Hand, Bank Balance, Receivables, Payables, Net Profit, Recent Vouchers).
- **Import / Export**: Full CSV and JSON data export and migration engine.
- **Local Backup & Restore**: One-click full database `.bak` archiving with automatic exit reminders.
- **Security & RBAC**: PBKDF2 HMAC-SHA256 password hashing (100,000 iterations), 4 roles (Administrator, Accountant, Operator, Viewer), and 20 granular permissions.
- **Audit Trail**: Immutable system event logging with CSV export.
- **Application Settings (F11)**: Customizable date formats, Indian (`₹ 12,34,567.89`) vs Western (`1,234,567.89`) number formatting, printer discovery, voucher lock date cutoff, and themes (`ClassicTeal`, `DarkSlate`, `LightNeutral`).

---

## ⌨️ Keyboard Shortcuts Reference

| Shortcut | Description |
| :--- | :--- |
| **Alt + G** / **Ctrl + K** / **Ctrl + F** | Go-To / Global Command Palette Search |
| **F2** | Change Date / Financial Year Period |
| **F3** | Select / Switch Active Company |
| **F4** | Open Contra Voucher Form |
| **F5** | Open Payment Voucher Form |
| **F6** | Open Receipt Voucher Form |
| **F7** | Open Journal Voucher Form |
| **F8** | Open Sales Voucher Form |
| **F9** | Open Purchase Voucher Form |
| **Ctrl + F8** | Open Credit Note Form |
| **Ctrl + F9** | Open Debit Note Form |
| **F10** | Open Backup & Restore Manager |
| **F11** | Open Application & Company Configuration |
| **Ctrl + S** | Save Active Voucher / Record |
| **Ctrl + N** | New Record / Clear Form |
| **Ctrl + P** | Print / Print Preview |
| **Esc** | Exit Dialog / Back to Gateway of Accounting |

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 or Windows 11 (64-bit)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or Desktop Runtime
- Microsoft SQL Server Express (2019 / 2022) with Windows Authentication

### Local Setup & Execution
1. **Clone the repository**:
   ```powershell
   git clone https://github.com/solankiparth2126-cell/MoneyFlow-Desktop.git
   cd MoneyFlow-Desktop
   ```

2. **Verify SQL Server Connection**:
   Update `MoneyFlow.Desktop\appsettings.json` if using a custom SQL instance:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=.\\SQLEXPRESS;Database=MoneyFlowDB;Trusted_Connection=True;TrustServerCertificate=True;"
     }
   }
   ```

3. **Build the solution**:
   ```powershell
   dotnet build MoneyFlow.sln
   ```

4. **Run the automated test suite**:
   ```powershell
   dotnet test
   ```
   *Expected result: 183 / 183 tests passing.*

5. **Launch the desktop application**:
   ```powershell
   dotnet run --project MoneyFlow.Desktop\MoneyFlow.Desktop.csproj
   ```

---

## 📦 Packaging & Installation

### Option 1: Standalone Application Distribution
Compile a standalone Windows release bundle into `./publish/`:
```powershell
dotnet publish MoneyFlow.Desktop\MoneyFlow.Desktop.csproj -c Release -r win-x64 --self-contained false -o .\publish\
```
Launch directly via `.\publish\MoneyFlow.Desktop.exe`.

### Option 2: Windows Installer (`MoneyFlowSetup.exe`)
The project includes an Inno Setup configuration at [`installer/MoneyFlowSetup.iss`](installer/MoneyFlowSetup.iss):
- Target Directory: `C:\Program Files\MoneyFlow\`
- Windows Shortcuts: Start Menu program group and Desktop shortcut
- Build automation script:
  ```powershell
  .\installer\build-installer.ps1
  ```

### Option 3: Direct SQL Server Scripts
For database administrators preferring direct SSMS deployment, complete T-SQL scripts are provided in [`database/scripts/`](database/scripts/):
1. `01_CreateDatabaseAndTables.sql`: Database creation and full schema DDL
2. `02_CreateIndexesAndConstraints.sql`: Performance and compound index tuning
3. `03_SeedSystemData.sql`: Default voucher types, system roles, and administrator user
4. `04_BackupAndRestore.sql`: T-SQL automated backup and restore stored procedures

---

## 🛡️ License & Attributions

MoneyFlow Desktop ERP is an original software development project created for educational and commercial desktop accounting workflows. Designed in strict compliance with standard double-entry bookkeeping and GAAP principles.
