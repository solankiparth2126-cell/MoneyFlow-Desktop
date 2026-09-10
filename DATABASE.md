# MoneyFlowDB Database Architecture (Phase 2)

## Overview
- **Database Engine**: Microsoft SQL Server Express (2019 / 2022 / 2025) or LocalDB
- **Database Name**: `MoneyFlowDB`
- **ORM**: Entity Framework Core 8 with Code-First migrations and design-time factory
- **DDL Script**: `MoneyFlow.Data/Scripts/CreateDatabaseAndTables.sql`

---

## Core Accounting Tables

### 1. Companies
Primary tenant entity for complete multi-company isolation.
- `CompanyId` (INT, PK, Identity)
- `CompanyName` (NVARCHAR(150), Not Null, Indexed)
- `Address` (NVARCHAR(250), Null)
- `State` (NVARCHAR(100), Null)
- `Country` (NVARCHAR(100), Default 'India')
- `PAN` (NVARCHAR(20), Null)
- `Email` (NVARCHAR(100), Null)
- `Phone` (NVARCHAR(50), Null)
- `FinancialYearFrom` (DATETIME2, Not Null)
- `BooksBeginningFrom` (DATETIME2, Not Null)
- `Currency` (NVARCHAR(10), Default '₹')
- `CreatedAt` (DATETIME2, Default UTC)
- `UpdatedAt` (DATETIME2, Null)
- `IsActive` (BIT, Default 1)

### 2. FinancialYears
Indian Financial Year accounting periods (e.g. 01-Apr-2026 to 31-Mar-2027).
- `FinancialYearId` (INT, PK, Identity)
- `CompanyId` (INT, FK -> Companies, On Delete Restrict)
- `YearName` (NVARCHAR(50), Not Null)
- `StartDate` (DATETIME2, Not Null)
- `EndDate` (DATETIME2, Not Null)
- `IsClosed` (BIT, Default 0)
- `CreatedAt` (DATETIME2, Default UTC)
- **Index**: `(CompanyId, StartDate, EndDate)`

### 3. Groups (Chart of Accounts Tree)
Hierarchical parent-child group structure supporting unlimited nesting.
- `GroupId` (INT, PK, Identity)
- `CompanyId` (INT, FK -> Companies, On Delete Restrict)
- `GroupName` (NVARCHAR(100), Not Null)
- `ParentGroupId` (INT, Null, Self-FK -> Groups, On Delete Restrict)
- `Nature` (INT, 1=Assets, 2=Liabilities, 3=Income, 4=Expenses)
- `PrimaryGroup` (BIT, Default 0)
- `AffectProfitLoss` (BIT, Default 0)
- `CreatedAt` (DATETIME2, Default UTC)
- `UpdatedAt` (DATETIME2, Null)
- `IsActive` (BIT, Default 1)
- **Indexes**: `(CompanyId, GroupName)`, `ParentGroupId`

### 4. Ledgers
Individual accounting ledgers belonging to a group and company.
- `LedgerId` (INT, PK, Identity)
- `CompanyId` (INT, FK -> Companies, On Delete Restrict)
- `GroupId` (INT, FK -> Groups, On Delete Restrict)
- `LedgerName` (NVARCHAR(150), Not Null)
- `OpeningBalance` (DECIMAL(18,2), Default 0.00)
- `OpeningBalanceType` (INT, 1=Dr, 2=Cr)
- `CreditLimit` (DECIMAL(18,2), Default 0.00)
- `CreditDays` (INT, Default 0)
- `Address`, `Phone`, `Email`, `PAN`, `State`
- `BankName`, `BankAccountNumber`, `IFSC`
- `IsActive` (BIT, Default 1)
- **Indexes**: `(CompanyId, LedgerName)`, `GroupId`

### 5. VoucherTypes
Pre-configured and seeded standard accounting voucher types:
- `VoucherTypeId` (INT, PK, Identity)
- `Name` (NVARCHAR(50), Not Null)
- `Code` (NVARCHAR(10), Not Null, Unique)
- `Type` (INT, Enum: Payment=1, Receipt=2, Contra=3, Journal=4, Sales=5, Purchase=6, DebitNote=7, CreditNote=8)
- `Prefix` (NVARCHAR(10), Null)
- `NextNumber` (INT, Default 1)
- `IsActive` (BIT, Default 1)

### 6. Vouchers
Voucher transaction headers.
- `VoucherId` (INT, PK, Identity)
- `CompanyId` (INT, FK -> Companies, On Delete Restrict)
- `FinancialYearId` (INT, FK -> FinancialYears, On Delete Restrict)
- `VoucherTypeId` (INT, FK -> VoucherTypes, On Delete Restrict)
- `VoucherNumber` (NVARCHAR(50), Not Null)
- `VoucherDate` (DATETIME2, Not Null)
- `ReferenceNumber` (NVARCHAR(50), Null)
- `Narration` (NVARCHAR(1000), Null)
- `CreatedAt`, `ModifiedAt`, `CreatedBy`, `ModifiedBy`
- `IsDeleted` (BIT, Default 0)
- **Indexes**: `CompanyId`, `FinancialYearId`, `VoucherDate`, `(CompanyId, VoucherNumber)`

### 7. VoucherEntries
Individual debit and credit postings.
- `VoucherEntryId` (INT, PK, Identity)
- `VoucherId` (INT, FK -> Vouchers, On Delete Cascade)
- `LedgerId` (INT, FK -> Ledgers, On Delete Restrict)
- `Debit` (DECIMAL(18,2), Default 0.00, Check >= 0)
- `Credit` (DECIMAL(18,2), Default 0.00, Check >= 0)
- `Narration` (NVARCHAR(500), Null)
- **Constraints**:
  - `CHECK (Debit >= 0.00)`
  - `CHECK (Credit >= 0.00)`
  - `CHECK (Debit = 0.00 OR Credit = 0.00)`
- **Indexes**: `VoucherId`, `LedgerId`

---

## Supporting Tables
- `Units`: Inventory units of measure
- `StockItems`: Basic inventory items
- `Users`: Application operators with role foreign keys
- `Roles`: Security roles (e.g. Administrator)
- `Permissions`: Granular permission keys
- `AuditLogs`: Audit trail for accounting actions
- `Settings`: Key-value application configuration
- `BackupHistory`: Log of database backup archives
