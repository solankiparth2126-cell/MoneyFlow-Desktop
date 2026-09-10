# MASTER PROMPT
# BUILD MY OWN TALLY-LIKE WINDOWS DESKTOP ACCOUNTING SOFTWARE

You are a senior C#/.NET desktop software architect, Windows Forms developer, SQL Server database architect, accounting-system developer, UI/UX designer, and QA engineer.

Your task is to build a **professional standalone Windows desktop accounting application** from scratch.

The application must be my own software. It can be inspired by the workflow and usability of accounting applications such as TallyPrime, but it must NOT copy Tally's source code, proprietary UI assets, branding, logos, or copyrighted implementation.

The application name is:

# MoneyFlow Desktop Accounting

---

# 1. CORE REQUIREMENT

Build a **Windows-only standalone desktop accounting application**.

The application is intended to run on:

```text
One Windows PC
One local SQL Server database
Offline
Single user initially
No cloud
No web server
No browser
No mobile application
```

The application must work even when there is no Internet connection.

---

# 2. TECHNOLOGY — DO NOT CHANGE

Use exactly:

```text
Language:
C#

Framework:
.NET 8

Desktop UI:
Windows Forms

Operating System:
Windows only

Database:
Microsoft SQL Server Express

Database Management:
SQL Server Management Studio (SSMS)

ORM:
Entity Framework Core

Reports:
RDLC

Installer:
Inno Setup
```

Do NOT convert this project to:

```text
WPF
MAUI
Blazor
ASP.NET
React
Next.js
Electron
Java
Python
SQLite
MongoDB
PostgreSQL
```

The application must remain:

```text
C# + .NET 8 + WinForms + SQL Server
```

---

# 3. SINGLE-PC ARCHITECTURE

This is NOT a multi-server system.

The target architecture is:

```text
Windows PC
│
├── MoneyFlow Desktop.exe
│
├── SQL Server Express
│
│    └── MoneyFlowDB
│
└── Local Backup Folder
```

Application:

```text
WinForms
   ↓
Services
   ↓
Entity Framework Core
   ↓
SQL Server Express
```

The database will be managed using:

```text
SQL Server Management Studio
```

---

# 4. DATABASE

Create one database:

```text
MoneyFlowDB
```

Use SQL Server Express.

The application must support multiple companies inside the same database.

Example:

```text
MoneyFlowDB
│
├── Company A
│
├── Company B
│
└── Company C
```

All company-specific records must contain:

```text
CompanyId
```

The current company must always be part of the application context.

A company must never be able to see another company's accounting data.

---

# 5. NO GST

GST IS COMPLETELY OUT OF SCOPE.

Do NOT create:

```text
GST
GSTIN
HSN
SAC
CGST
SGST
IGST
GST API
GST Fetch
GST Validation
GST Return
GST Report
GST Integration
```

Do not create GST database columns unless absolutely required for some unrelated generic future architecture.

Do not create a GST module.

This is a pure accounting application.

---

# 6. MAIN OBJECTIVE

Build an accounting system with this workflow:

```text
Company
    ↓
Financial Year
    ↓
Groups
    ↓
Ledgers
    ↓
Vouchers
    ↓
Accounting Engine
    ↓
Ledger
    ↓
Trial Balance
    ↓
Profit & Loss
    ↓
Balance Sheet
```

The accounting engine is the most important part of the application.

---

# 7. ACCOUNTING PRINCIPLE

Implement proper double-entry accounting.

Every accounting transaction must contain debit and credit entries.

The fundamental rule is:

```text
Total Debit = Total Credit
```

An unbalanced voucher MUST NOT be saved.

Example:

```text
Cash A/c             Dr ₹500
      To Food Expense       ₹500
```

Database entries:

```text
Cash
Debit = 500
Credit = 0

Food Expense
Debit = 0
Credit = 500
```

Voucher validation:

```text
Total Debit  = ₹500
Total Credit = ₹500

Difference = ₹0
```

Allow save.

If:

```text
Debit = ₹500
Credit = ₹400
```

show:

```text
Voucher is not balanced.

Debit  : ₹500
Credit : ₹400
Difference : ₹100
```

Disable Save.

---

# 8. DO NOT STORE CALCULATED BALANCES MANUALLY

Do not depend on manually updated ledger balances.

The source of truth must be:

```text
Voucher
    ↓
VoucherEntries
    ↓
Accounting calculations
```

Ledger balance should be calculated from:

```text
Opening Balance
+
Accounting Entries
=
Closing Balance
```

Reports must be generated from accounting transactions.

---

# 9. PROJECT ARCHITECTURE

Create:

```text
MoneyFlow.sln

MoneyFlow.Desktop
MoneyFlow.Core
MoneyFlow.Data
MoneyFlow.Services
MoneyFlow.Reports
MoneyFlow.Tests
```

Structure:

```text
MoneyFlow.Desktop
│
├── Forms
├── Controls
├── Dialogs
├── Resources
├── Reports
└── Program.cs

MoneyFlow.Core
│
├── Entities
├── DTOs
├── Enums
├── Interfaces
├── Constants
└── Exceptions

MoneyFlow.Data
│
├── AppDbContext.cs
├── Configurations
├── Repositories
├── Migrations
└── Seed

MoneyFlow.Services
│
├── Company
├── FinancialYear
├── Group
├── Ledger
├── Voucher
├── Accounting
├── Reports
├── Backup
├── Import
├── Export
└── Validation

MoneyFlow.Reports
│
└── RDLC

MoneyFlow.Tests
│
├── AccountingTests
├── VoucherTests
├── LedgerTests
└── ReportTests
```

---

# 10. DATABASE TABLES

Create these tables:

```text
Companies
FinancialYears
Groups
Ledgers
VoucherTypes
Vouchers
VoucherEntries
StockItems
Units
Users
Roles
Permissions
AuditLogs
Settings
BackupHistory
```

Do NOT create GST tables.

---

# 11. COMPANY MANAGEMENT

Create:

```text
Create Company
Open Company
Alter Company
Close Company
Delete Company
Backup Company
Change Company
```

Company fields:

```text
CompanyId
CompanyName
Address
State
Country
PAN
Email
Phone
FinancialYearFrom
BooksBeginningFrom
Currency
CreatedAt
UpdatedAt
IsActive
```

When creating a company:

```text
Company Name:
Address:
State:
Country:
PAN:
Email:
Phone:

Financial Year:
01-Apr-2026 to 31-Mar-2027

Books Beginning From:
01-Apr-2026
```

---

# 12. FINANCIAL YEAR

Support Indian financial years.

Example:

```text
2026-27

01-Apr-2026
to
31-Mar-2027
```

Every voucher must belong to a financial year.

Do not allow transactions outside the current financial year unless the user explicitly changes the financial year.

---

# 13. GROUP MASTER

Create hierarchical groups.

Default groups:

```text
Capital Account
Current Assets
Current Liabilities
Fixed Assets
Investments
Loans
Bank Accounts
Cash-in-Hand
Sundry Debtors
Sundry Creditors
Direct Expenses
Indirect Expenses
Direct Income
Indirect Income
Sales Accounts
Purchase Accounts
Duties & Taxes
```

Group fields:

```text
GroupId
CompanyId
GroupName
ParentGroupId
Nature
PrimaryGroup
AffectProfitLoss
CreatedAt
UpdatedAt
IsActive
```

Support:

```text
Group
    ↓
Sub Group
    ↓
Sub Group
```

Example:

```text
Current Assets
    └── Bank Accounts
          ├── HDFC Bank
          └── SBI Bank
```

---

# 14. LEDGER MASTER

Create:

```text
Ledger Master
```

Fields:

```text
LedgerId
CompanyId
GroupId
LedgerName
OpeningBalance
OpeningBalanceType
Address
Phone
Email
PAN
State
CreditLimit
CreditDays
BankName
BankAccountNumber
IFSC
IsActive
CreatedAt
UpdatedAt
```

Opening balance:

```text
₹10,000 Dr

or

₹5,000 Cr
```

Do not store calculated closing balance as the authoritative value.

---

# 15. DEFAULT LEDGERS

When creating a company, ask:

```text
Create default accounting ledgers?

Yes / No
```

If Yes:

```text
Cash
Profit & Loss A/c
Capital Account
Sales
Purchase
Sundry Debtors
Sundry Creditors
Direct Expenses
Indirect Expenses
Direct Income
Indirect Income
```

---

# 16. VOUCHER TYPES

Create:

```text
Payment
Receipt
Contra
Journal
Sales
Purchase
Debit Note
Credit Note
```

Voucher table:

```text
VoucherId
CompanyId
FinancialYearId
VoucherTypeId
VoucherNumber
VoucherDate
ReferenceNumber
Narration
CreatedAt
ModifiedAt
CreatedBy
ModifiedBy
IsDeleted
```

---

# 17. VOUCHER ENTRIES

Create:

```text
VoucherEntryId
VoucherId
LedgerId
Debit
Credit
Narration
```

Rules:

```text
Debit >= 0
Credit >= 0
Debit and Credit cannot both be > 0
At least two accounting entries
Total Debit = Total Credit
```

---

# 18. ACCOUNTING ENGINE

Create:

```text
AccountingService
VoucherService
LedgerBalanceService
TrialBalanceService
ProfitLossService
BalanceSheetService
```

The accounting engine must be independent of the UI.

Do not write accounting logic directly inside WinForms button events.

Example:

```text
frmPayment
     ↓
VoucherService
     ↓
AccountingService
     ↓
EF Core
     ↓
SQL Server
```

---

# 19. DATABASE TRANSACTIONS

Saving a voucher must use a SQL database transaction.

Process:

```text
Begin Transaction

Validate Company
Validate Financial Year
Validate Voucher
Validate Ledgers
Validate Debit/Credit
Validate Balance

Insert Voucher
Insert Voucher Entries

Recalculate/validate accounting integrity

Commit
```

If anything fails:

```text
Rollback
```

Never leave half-created vouchers in the database.

---

# 20. PAYMENT

Example:

```text
Payment No: P-00001
Date: 10-Sep-2026

Food Expense      Dr ₹500
      To Cash           ₹500
```

Create:

```text
frmPayment
```

Features:

```text
New
Save
Save & New
Edit
Delete
Print
Cancel
```

---

# 21. RECEIPT

Example:

```text
Receipt No: R-00001

Cash              Dr ₹10,000
      To Income         ₹10,000
```

Create:

```text
frmReceipt
```

---

# 22. CONTRA

Support:

```text
Cash → Bank
Bank → Cash
Bank → Bank
```

Create:

```text
frmContra
```

---

# 23. JOURNAL

Support adjustment entries.

Example:

```text
Depreciation Expense    Dr ₹2,000
      To Furniture            ₹2,000
```

Create:

```text
frmJournal
```

Support multiple debit/credit lines.

---

# 24. SALES

Support:

```text
Customer
Sales Ledger
Item
Quantity
Rate
Discount
Total
```

Do NOT add GST.

Example:

```text
Customer: ABC Traders

Item: Product A
Quantity: 10
Rate: ₹100
Discount: ₹50

Total: ₹950
```

---

# 25. PURCHASE

Support:

```text
Supplier
Purchase Ledger
Item
Quantity
Rate
Discount
Total
```

Do NOT add GST.

---

# 26. INVENTORY

Implement basic inventory only after the accounting engine is stable.

Tables:

```text
StockItems
Units
```

Stock item:

```text
StockItemId
CompanyId
ItemName
UnitId
OpeningQuantity
OpeningRate
OpeningValue
IsActive
CreatedAt
UpdatedAt
```

Support:

```text
Quantity
Rate
Amount
Closing Stock
```

Advanced inventory features are not required in the first release.

---

# 27. MAIN SCREEN

Create a Tally-inspired but original interface.

Main screen:

```text
MONEYFLOW DESKTOP ERP

Gateway of Accounting

Company
Masters
Transactions
Reports
Utilities
Exit
```

Show current context:

```text
Company:
ABC Traders

Financial Year:
2026-27
```

---

# 28. MASTERS MENU

```text
Masters

Groups
Ledgers
Stock Items
Units
Voucher Types
```

---

# 29. TRANSACTIONS MENU

```text
Transactions

Payment
Receipt
Contra
Journal
Sales
Purchase
Debit Note
Credit Note
```

---

# 30. REPORTS MENU

```text
Reports

Day Book
Ledger
Trial Balance
Profit & Loss
Balance Sheet
Cash Book
Bank Book
Outstanding
Stock Summary
```

---

# 31. UTILITIES MENU

```text
Utilities

Import
Export
Backup
Restore
Settings
Database Tools
```

---

# 32. KEYBOARD SHORTCUTS

Implement:

```text
F2 = Change Date
F4 = Contra
F5 = Payment
F6 = Receipt
F7 = Journal
F8 = Sales
F9 = Purchase
Esc = Back/Cancel

Ctrl+S = Save
Ctrl+N = New
Ctrl+F = Search
Ctrl+P = Print
```

Make accounting entry possible with minimal mouse use.

---

# 33. VOUCHER ENTRY UI

Create reusable:

```text
VoucherEntryControl
```

Header:

```text
Voucher Type
Voucher Number
Date
Reference Number
Narration
```

Grid:

```text
Ledger              Debit       Credit
---------------------------------------
Cash                5,000
Food Expense                    5,000
```

Footer:

```text
Total Debit
Total Credit
Difference
```

Display:

```text
BALANCED
```

or:

```text
NOT BALANCED
```

Save must be disabled while unbalanced.

---

# 34. LEDGER REPORT

Display:

```text
ABC TRADERS

Date
Particulars
Voucher No
Debit
Credit
Balance
```

Features:

```text
Date Filter
Voucher Drill Down
Print
PDF
Excel
CSV
```

Double-click a voucher to open it.

---

# 35. DAY BOOK

Columns:

```text
Date
Voucher No
Voucher Type
Particulars
Debit
Credit
Narration
```

Filters:

```text
From Date
To Date
Voucher Type
Ledger
```

---

# 36. TRIAL BALANCE

Display:

```text
Ledger
Debit
Credit
```

At bottom:

```text
Total Debit
Total Credit
Difference
```

Must verify:

```text
Debit = Credit
```

If not:

```text
ACCOUNTING INTEGRITY ERROR
```

---

# 37. PROFIT & LOSS

Calculate:

```text
Income

Sales
Other Income

Less:

Direct Expenses
Indirect Expenses

------------------

Net Profit / Net Loss
```

Allow date/financial-year selection.

---

# 38. BALANCE SHEET

Liabilities:

```text
Capital
Loans
Sundry Creditors
Current Liabilities
Duties & Taxes
Profit/Loss
```

Assets:

```text
Fixed Assets
Investments
Sundry Debtors
Cash
Bank
Current Assets
```

Validate:

```text
Total Assets = Total Liabilities
```

---

# 39. CASH BOOK

Show:

```text
Opening Cash
Receipts
Payments
Closing Cash
```

Allow date filtering.

---

# 40. BANK BOOK

Allow selecting:

```text
HDFC Bank
SBI Bank
Kotak Bank
etc.
```

Show:

```text
Opening Balance
Deposits
Withdrawals
Closing Balance
```

---

# 41. OUTSTANDING

Customer:

```text
Party
Invoice
Date
Due Date
Amount
Received
Outstanding
Overdue Days
```

Supplier:

```text
Party
Invoice
Date
Due Date
Amount
Paid
Outstanding
Overdue Days
```

Ageing:

```text
0–30 Days
31–60 Days
61–90 Days
90+ Days
```

---

# 42. SEARCH

Create global search.

Search:

```text
Ledger
Voucher
Party
Amount
Date
Voucher Number
Narration
```

Results must support:

```text
Double-click → Open Record
```

---

# 43. BACKUP

Implement local backup.

Default folder:

```text
Documents\MoneyFlow\Backups
```

Example:

```text
ABC Traders_2026-09-10_1015.bak
```

or another safe backup format appropriate for SQL Server Express.

Support:

```text
Manual Backup
Automatic Backup
Restore
Backup History
```

Never silently overwrite an existing backup.

---

# 44. IMPORT

Support:

```text
Excel
CSV
Bank Statement
```

Process:

```text
Select File
     ↓
Map Columns
     ↓
Preview
     ↓
Validate
     ↓
Detect Duplicates
     ↓
User Confirmation
     ↓
Import
```

Never directly insert unvalidated imported data into accounting tables.

---

# 45. EXPORT

Support:

```text
Excel
CSV
PDF
Print
```

---

# 46. AUDIT LOG

Record:

```text
Create
Edit
Delete
Restore
Login
Backup
Restore
Import
Export
```

Audit fields:

```text
UserId
Action
Module
RecordId
Timestamp
Description
```

Use soft delete for accounting transactions.

---

# 47. USER SYSTEM

Although the application initially targets one PC and one user, design the database so users can be added later.

Create:

```text
Users
Roles
Permissions
```

Initial role:

```text
Administrator
```

Later:

```text
Accountant
Operator
Viewer
```

Never store plain-text passwords.

---

# 48. APPLICATION SETTINGS

Create:

```text
Settings
```

Support:

```text
Company
Current Financial Year
Backup Folder
Printer
Date Format
Number Format
Application Theme
```

---

# 49. ERROR HANDLING

Never expose raw technical errors to the user.

Instead:

```text
Unable to save voucher.

Please check the voucher entries and try again.
```

Technical logs should contain:

```text
Timestamp
Module
Exception
Stack Trace
User
```

Do not log passwords.

---

# 50. PERFORMANCE

Optimize for a standalone accounting PC.

Use:

```text
EF Core AsNoTracking()
Indexes
Pagination
Efficient queries
Async database operations where useful
SQL transactions
```

Important indexes:

```text
CompanyId
FinancialYearId
VoucherDate
VoucherNumber
LedgerId
VoucherId
```

---

# 51. DATA INTEGRITY

Always enforce:

```text
Voucher belongs to Company
Voucher belongs to Financial Year
Voucher Entry belongs to Voucher
Voucher Entry references valid Ledger
Ledger belongs to current Company
Group belongs to current Company
```

Prevent cross-company data access.

---

# 52. ACCOUNTING TEST CASES

Create automated tests.

Test:

```text
Opening Balance
Payment
Receipt
Contra
Journal
Sales
Purchase
Debit Note
Credit Note
Ledger Balance
Trial Balance
Profit & Loss
Balance Sheet
```

Example:

```text
Cash Dr ₹1,000
    To Income ₹1,000
```

Expected:

```text
Debit = ₹1,000
Credit = ₹1,000
Difference = ₹0
```

Unbalanced:

```text
Cash Dr ₹1,000
```

Expected:

```text
Voucher rejected
```

---

# 53. MULTI-COMPANY TEST

Create:

```text
Company A
Company B
```

Create:

```text
Cash A
```

inside Company A.

Switch to Company B.

Verify:

```text
Cash A
```

cannot appear.

This is mandatory.

---

# 54. FINANCIAL YEAR TEST

Create:

```text
2025-26
2026-27
```

Create a voucher in 2025-26.

Open 2026-27.

Verify that the voucher does not appear in normal 2026-27 reports.

---

# 55. UI DESIGN

The application should be:

```text
Professional
Simple
Fast
Keyboard friendly
Accounting focused
```

Do not create a flashy web-style interface.

Use:

```text
MenuStrip
ToolStrip
Panels
DataGridView
GroupBox
TabControl
Dialogs
StatusStrip
```

Use consistent spacing, fonts and controls.

---

# 56. REPORT DESIGN

Reports must look professional.

Header:

```text
Company Name
Company Address

Report Name

Period:
01-Apr-2026 to 31-Mar-2027
```

Footer:

```text
Generated:
Page X of Y
```

Use RDLC.

---

# 57. PRINTING

Support:

```text
Print Preview
Print
PDF
```

Allow printer selection through Windows printer configuration.

---

# 58. INSTALLATION

Create an Inno Setup installer:

```text
MoneyFlowSetup.exe
```

Installation should create:

```text
C:\Program Files\MoneyFlow\
```

and shortcuts:

```text
Desktop
Start Menu
```

Do not install unnecessary components.

---

# 59. SQL SERVER SETUP

The application should support a local SQL Server Express instance.

Example:

```text
.\SQLEXPRESS
```

But do not hard-code the instance name.

Allow database connection configuration.

Example:

```text
Server:
.\SQLEXPRESS

Database:
MoneyFlowDB

Authentication:
Windows Authentication
```

Prefer Windows Authentication for the initial standalone deployment.

---

# 60. FIRST-RUN DATABASE SETUP

When the application launches:

```text
Check SQL Server connection
        ↓
Check MoneyFlowDB
        ↓
If database does not exist
        ↓
Create / migrate database
        ↓
Seed required system data
        ↓
Launch application
```

If SQL Server cannot be reached:

```text
Database connection failed.

Please check that SQL Server is running.
```

Provide a diagnostic button.

---

# 61. DATABASE MANAGEMENT

The developer must provide SQL scripts where appropriate:

```text
Create Database
Create Tables
Indexes
Seed Data
Backup
Restore
```

EF Core migrations remain the primary schema management method.

SSMS should be usable for administration and inspection.

---

# 62. NO CLOUD DEPENDENCY

The application must not require:

```text
Internet
Cloud login
Online API
Cloud database
```

for normal accounting operations.

Everything must work locally.

---

# 63. SECURITY

Use:

```text
SQL parameters / EF Core
Input validation
Password hashing
Authorization
Company isolation
Transactions
Audit logs
Backup
```

Never concatenate user input into SQL queries.

---

# 64. SOURCE CONTROL

Use Git.

Recommended:

```text
main
develop
feature/*
bugfix/*
```

Use meaningful commits:

```text
feat: add ledger master
feat: add payment voucher
fix: prevent unbalanced voucher
feat: add trial balance report
```

---

# 65. DOCUMENTATION

Create:

```text
README.md
ARCHITECTURE.md
DATABASE.md
ACCOUNTING_ENGINE.md
INSTALLATION.md
USER_GUIDE.md
TESTING.md
DEPLOYMENT.md
CHANGELOG.md
```

---

# 66. DEVELOPMENT METHOD

DO NOT generate the entire application in one response.

Build it incrementally.

For every phase:

1. Explain the objective.
2. Show files to create.
3. Show folder structure.
4. Create database changes.
5. Create C# models.
6. Create services.
7. Create WinForms UI.
8. Add validation.
9. Add error handling.
10. Compile the project.
11. Fix errors.
12. Test functionality.
13. Give a test checklist.
14. Wait for confirmation.

Never skip testing.

---

# 67. DEVELOPMENT PHASES

## PHASE 1 — Environment & Project Setup

Check:

```text
.NET SDK
Visual Studio
SQL Server Express
SSMS
EF Core
```

Create:

```text
Solution
Projects
Folders
NuGet packages
Configuration
DbContext
Connection string
Initial migration
Database
```

---

## PHASE 2 — Database Foundation

Create:

```text
Companies
FinancialYears
Groups
Ledgers
VoucherTypes
Vouchers
VoucherEntries
```

Add:

```text
Primary Keys
Foreign Keys
Indexes
Constraints
Relationships
```

---

## PHASE 3 — Company Management

Implement:

```text
Create
Open
Alter
Close
Delete
Switch Company
Backup
```

---

## PHASE 4 — Financial Year

Implement:

```text
Create FY
Open FY
Change FY
Validate dates
```

---

## PHASE 5 — Group Master

Implement:

```text
Create
Edit
Delete
Search
Nested Groups
```

---

## PHASE 6 — Ledger Master

Implement:

```text
Create
Edit
Delete
Opening Balance
Search
Ledger selection
```

---

## PHASE 7 — ACCOUNTING ENGINE

This is a critical milestone.

Implement:

```text
Debit/Credit
Voucher validation
Ledger calculation
Opening balance
Closing balance
Trial balance calculation
```

Test thoroughly before continuing.

---

## PHASE 8 — Payment

---

## PHASE 9 — Receipt

---

## PHASE 10 — Contra

---

## PHASE 11 — Journal

---

## PHASE 12 — Sales

---

## PHASE 13 — Purchase

---

## PHASE 14 — Debit Note

---

## PHASE 15 — Credit Note

---

## PHASE 16 — Day Book

---

## PHASE 17 — Ledger Reports

---

## PHASE 18 — Trial Balance

---

## PHASE 19 — Profit & Loss

---

## PHASE 20 — Balance Sheet

---

## PHASE 21 — Cash Book

---

## PHASE 22 — Bank Book

---

## PHASE 23 — Outstanding

---

## PHASE 24 — Inventory

---

## PHASE 25 — Global Search

---

## PHASE 26 — Dashboard

---

## PHASE 27 — Import / Export

---

## PHASE 28 — Backup / Restore

---

## PHASE 29 — Users / Permissions

---

## PHASE 30 — Audit Logging

---

## PHASE 31 — Testing

---

## PHASE 32 — Performance Optimization

---

## PHASE 33 — UI/UX Polish

---

## PHASE 34 — Installer

---

## PHASE 35 — Final QA

---

# 68. PHASE COMPLETION RULE

At the end of every phase provide:

```text
PHASE STATUS

[ ] Code complete
[ ] Database complete
[ ] Build successful
[ ] No compile errors
[ ] Unit tests passed
[ ] Manual tests passed
[ ] Error handling tested
[ ] Backup tested where applicable
[ ] Documentation updated
```

Then stop.

Wait for:

```text
PHASE X COMPLETE
```

before continuing.

---

# 69. IMPORTANT DEVELOPMENT RULES

Never:

```text
Create fake functionality
Ignore compile errors
Ignore database errors
Skip accounting validation
Store passwords in plain text
Hard-code credentials
Use floating point for money
Put business logic in forms
Duplicate accounting calculations
Delete accounting records permanently by default
```

Use:

```text
decimal
EF Core
SQL transactions
Dependency Injection
Interfaces
Services
DTOs
Validation
Logging
Unit Tests
```

---

# 70. MONEY CALCULATIONS

Use:

```text
decimal
```

for all monetary calculations.

Never use:

```text
float
double
```

for financial amounts.

Use appropriate SQL decimal precision.

Example:

```text
decimal(18,2)
```

---

# 71. FINAL PRODUCT

The final application should be a genuine standalone Windows accounting program:

```text
MoneyFlow Desktop ERP
```

with:

```text
Company Management
Financial Years
Groups
Ledgers
Double Entry Accounting
Payment
Receipt
Contra
Journal
Sales
Purchase
Debit Note
Credit Note
Basic Inventory
Day Book
Ledger
Trial Balance
Profit & Loss
Balance Sheet
Cash Book
Bank Book
Outstanding
Search
Dashboard
Import
Export
Backup
Restore
Users
Permissions
Audit Logs
Printing
PDF
Excel
CSV
Windows Installer
```

And explicitly:

```text
NO GST
NO GST FETCH
NO GST API
NO WEB APPLICATION
NO CLOUD
NO MOBILE APP
NO MULTI-PC NETWORK REQUIREMENT
```

---

# 72. START NOW

Start with:

# PHASE 1 ONLY

First inspect the Windows development environment.

Check:

```powershell
dotnet --version

dotnet --list-sdks

Get-Service | Where-Object {$_.Name -like "*SQL*"} |
Select-Object Name, Status

sqlcmd -?
```

Also determine whether Visual Studio is installed.

Do not assume the SQL Server instance name.

Do not modify or uninstall anything.

After checking the environment, create the Phase 1 solution and project structure.

At the end of Phase 1, provide:

```text
PHASE 1 COMPLETE CHECKLIST
```

Then STOP.

Wait for the user to say:

PHASE 1 COMPLETE

Only after that should you start Phase 2.

# END OF MASTER PROMPT