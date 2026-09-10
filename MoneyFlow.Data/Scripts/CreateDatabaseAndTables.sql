-- ===================================================================================
-- MONEYFLOW DESKTOP ACCOUNTING (MoneyFlowDB)
-- PURE DOUBLE-ENTRY ACCOUNTING RELATIONAL DATABASE DDL SCRIPT
-- Compatible with SQL Server 2019 / 2022 / 2025 Express & Enterprise
-- ===================================================================================

-- 1. CREATE DATABASE
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'MoneyFlowDB')
BEGIN
    CREATE DATABASE MoneyFlowDB;
END
GO

USE MoneyFlowDB;
GO

-- 2. COMPANIES
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Companies')
BEGIN
    CREATE TABLE Companies (
        CompanyId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CompanyName NVARCHAR(150) NOT NULL,
        Address NVARCHAR(250) NULL,
        State NVARCHAR(100) NULL,
        Country NVARCHAR(100) NOT NULL CONSTRAINT DF_Companies_Country DEFAULT ('India'),
        PAN NVARCHAR(20) NULL,
        Email NVARCHAR(100) NULL,
        Phone NVARCHAR(50) NULL,
        FinancialYearFrom DATETIME2 NOT NULL,
        BooksBeginningFrom DATETIME2 NOT NULL,
        Currency NVARCHAR(10) NOT NULL CONSTRAINT DF_Companies_Currency DEFAULT (N'₹'),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Companies_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2 NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Companies_IsActive DEFAULT (1)
    );

    CREATE INDEX IX_Companies_CompanyName ON Companies (CompanyName);
END
GO

-- 3. FINANCIAL YEARS
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FinancialYears')
BEGIN
    CREATE TABLE FinancialYears (
        FinancialYearId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CompanyId INT NOT NULL CONSTRAINT FK_FinancialYears_Companies FOREIGN KEY REFERENCES Companies(CompanyId),
        YearName NVARCHAR(50) NOT NULL,
        StartDate DATETIME2 NOT NULL,
        EndDate DATETIME2 NOT NULL,
        IsClosed BIT NOT NULL CONSTRAINT DF_FinancialYears_IsClosed DEFAULT (0),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_FinancialYears_CreatedAt DEFAULT (SYSUTCDATETIME())
    );

    CREATE INDEX IX_FinancialYears_Company_Dates ON FinancialYears (CompanyId, StartDate, EndDate);
END
GO

-- 4. GROUPS (Chart of Accounts Hierarchical Group Master)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Groups')
BEGIN
    CREATE TABLE Groups (
        GroupId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CompanyId INT NOT NULL CONSTRAINT FK_Groups_Companies FOREIGN KEY REFERENCES Companies(CompanyId),
        GroupName NVARCHAR(100) NOT NULL,
        ParentGroupId INT NULL CONSTRAINT FK_Groups_ParentGroup FOREIGN KEY REFERENCES Groups(GroupId),
        Nature INT NOT NULL, -- 1=Assets, 2=Liabilities, 3=Income, 4=Expenses
        PrimaryGroup BIT NOT NULL CONSTRAINT DF_Groups_PrimaryGroup DEFAULT (0),
        AffectProfitLoss BIT NOT NULL CONSTRAINT DF_Groups_AffectProfitLoss DEFAULT (0),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Groups_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2 NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Groups_IsActive DEFAULT (1)
    );

    CREATE INDEX IX_Groups_Company_Name ON Groups (CompanyId, GroupName);
    CREATE INDEX IX_Groups_ParentGroupId ON Groups (ParentGroupId);
END
GO

-- 5. LEDGERS
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Ledgers')
BEGIN
    CREATE TABLE Ledgers (
        LedgerId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CompanyId INT NOT NULL CONSTRAINT FK_Ledgers_Companies FOREIGN KEY REFERENCES Companies(CompanyId),
        GroupId INT NOT NULL CONSTRAINT FK_Ledgers_Groups FOREIGN KEY REFERENCES Groups(GroupId),
        LedgerName NVARCHAR(150) NOT NULL,
        OpeningBalance DECIMAL(18,2) NOT NULL CONSTRAINT DF_Ledgers_OpeningBalance DEFAULT (0.00),
        OpeningBalanceType INT NOT NULL CONSTRAINT DF_Ledgers_OpeningType DEFAULT (1), -- 1=Dr, 2=Cr
        Address NVARCHAR(250) NULL,
        Phone NVARCHAR(50) NULL,
        Email NVARCHAR(100) NULL,
        PAN NVARCHAR(20) NULL,
        State NVARCHAR(100) NULL,
        CreditLimit DECIMAL(18,2) NOT NULL CONSTRAINT DF_Ledgers_CreditLimit DEFAULT (0.00),
        CreditDays INT NOT NULL CONSTRAINT DF_Ledgers_CreditDays DEFAULT (0),
        BankName NVARCHAR(100) NULL,
        BankAccountNumber NVARCHAR(50) NULL,
        IFSC NVARCHAR(20) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Ledgers_IsActive DEFAULT (1),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Ledgers_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2 NULL
    );

    CREATE INDEX IX_Ledgers_Company_Name ON Ledgers (CompanyId, LedgerName);
    CREATE INDEX IX_Ledgers_GroupId ON Ledgers (GroupId);
END
GO

-- 6. VOUCHER TYPES
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'VoucherTypes')
BEGIN
    CREATE TABLE VoucherTypes (
        VoucherTypeId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name NVARCHAR(50) NOT NULL,
        Code NVARCHAR(10) NOT NULL,
        Type INT NOT NULL, -- 1=Payment, 2=Receipt, 3=Contra, 4=Journal, 5=Sales, 6=Purchase, 7=DebitNote, 8=CreditNote
        Prefix NVARCHAR(10) NULL,
        NextNumber INT NOT NULL CONSTRAINT DF_VoucherTypes_NextNumber DEFAULT (1),
        IsActive BIT NOT NULL CONSTRAINT DF_VoucherTypes_IsActive DEFAULT (1)
    );

    CREATE UNIQUE INDEX UQ_VoucherTypes_Code ON VoucherTypes (Code);
END
GO

-- 7. VOUCHERS
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Vouchers')
BEGIN
    CREATE TABLE Vouchers (
        VoucherId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CompanyId INT NOT NULL CONSTRAINT FK_Vouchers_Companies FOREIGN KEY REFERENCES Companies(CompanyId),
        FinancialYearId INT NOT NULL CONSTRAINT FK_Vouchers_FinancialYears FOREIGN KEY REFERENCES FinancialYears(FinancialYearId),
        VoucherTypeId INT NOT NULL CONSTRAINT FK_Vouchers_VoucherTypes FOREIGN KEY REFERENCES VoucherTypes(VoucherTypeId),
        VoucherNumber NVARCHAR(50) NOT NULL,
        VoucherDate DATETIME2 NOT NULL,
        ReferenceNumber NVARCHAR(50) NULL,
        Narration NVARCHAR(1000) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Vouchers_CreatedAt DEFAULT (SYSUTCDATETIME()),
        ModifiedAt DATETIME2 NULL,
        CreatedBy NVARCHAR(50) NOT NULL CONSTRAINT DF_Vouchers_CreatedBy DEFAULT ('Admin'),
        ModifiedBy NVARCHAR(50) NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_Vouchers_IsDeleted DEFAULT (0)
    );

    CREATE INDEX IX_Vouchers_CompanyId ON Vouchers (CompanyId);
    CREATE INDEX IX_Vouchers_FinancialYearId ON Vouchers (FinancialYearId);
    CREATE INDEX IX_Vouchers_VoucherDate ON Vouchers (VoucherDate);
    CREATE INDEX IX_Vouchers_Company_Number ON Vouchers (CompanyId, VoucherNumber);
END
GO

-- 8. VOUCHER ENTRIES (Strict Double-Entry Enforcement)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'VoucherEntries')
BEGIN
    CREATE TABLE VoucherEntries (
        VoucherEntryId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        VoucherId INT NOT NULL CONSTRAINT FK_VoucherEntries_Vouchers FOREIGN KEY REFERENCES Vouchers(VoucherId) ON DELETE CASCADE,
        LedgerId INT NOT NULL CONSTRAINT FK_VoucherEntries_Ledgers FOREIGN KEY REFERENCES Ledgers(LedgerId),
        Debit DECIMAL(18,2) NOT NULL CONSTRAINT DF_VoucherEntries_Debit DEFAULT (0.00),
        Credit DECIMAL(18,2) NOT NULL CONSTRAINT DF_VoucherEntries_Credit DEFAULT (0.00),
        Narration NVARCHAR(500) NULL,
        CONSTRAINT CK_VoucherEntries_NonNegativeDebit CHECK (Debit >= 0.00),
        CONSTRAINT CK_VoucherEntries_NonNegativeCredit CHECK (Credit >= 0.00),
        CONSTRAINT CK_VoucherEntries_MutuallyExclusive CHECK (Debit = 0.00 OR Credit = 0.00)
    );

    CREATE INDEX IX_VoucherEntries_VoucherId ON VoucherEntries (VoucherId);
    CREATE INDEX IX_VoucherEntries_LedgerId ON VoucherEntries (LedgerId);
END
GO

-- 9. SEED DEFAULT SYSTEM DATA
SET IDENTITY_INSERT VoucherTypes ON;
IF NOT EXISTS (SELECT * FROM VoucherTypes WHERE Code = 'PMT')
    INSERT INTO VoucherTypes (VoucherTypeId, Name, Code, Type, Prefix, NextNumber, IsActive) VALUES (1, 'Payment', 'PMT', 1, 'PAY-', 1, 1);
IF NOT EXISTS (SELECT * FROM VoucherTypes WHERE Code = 'RCT')
    INSERT INTO VoucherTypes (VoucherTypeId, Name, Code, Type, Prefix, NextNumber, IsActive) VALUES (2, 'Receipt', 'RCT', 2, 'REC-', 1, 1);
IF NOT EXISTS (SELECT * FROM VoucherTypes WHERE Code = 'CNT')
    INSERT INTO VoucherTypes (VoucherTypeId, Name, Code, Type, Prefix, NextNumber, IsActive) VALUES (3, 'Contra', 'CNT', 3, 'CTR-', 1, 1);
IF NOT EXISTS (SELECT * FROM VoucherTypes WHERE Code = 'JRN')
    INSERT INTO VoucherTypes (VoucherTypeId, Name, Code, Type, Prefix, NextNumber, IsActive) VALUES (4, 'Journal', 'JRN', 4, 'JRN-', 1, 1);
IF NOT EXISTS (SELECT * FROM VoucherTypes WHERE Code = 'SLS')
    INSERT INTO VoucherTypes (VoucherTypeId, Name, Code, Type, Prefix, NextNumber, IsActive) VALUES (5, 'Sales', 'SLS', 5, 'SAL-', 1, 1);
IF NOT EXISTS (SELECT * FROM VoucherTypes WHERE Code = 'PUR')
    INSERT INTO VoucherTypes (VoucherTypeId, Name, Code, Type, Prefix, NextNumber, IsActive) VALUES (6, 'Purchase', 'PUR', 6, 'PUR-', 1, 1);
IF NOT EXISTS (SELECT * FROM VoucherTypes WHERE Code = 'DRN')
    INSERT INTO VoucherTypes (VoucherTypeId, Name, Code, Type, Prefix, NextNumber, IsActive) VALUES (7, 'Debit Note', 'DRN', 7, 'DRN-', 1, 1);
IF NOT EXISTS (SELECT * FROM VoucherTypes WHERE Code = 'CRN')
    INSERT INTO VoucherTypes (VoucherTypeId, Name, Code, Type, Prefix, NextNumber, IsActive) VALUES (8, 'Credit Note', 'CRN', 8, 'CRN-', 1, 1);
SET IDENTITY_INSERT VoucherTypes OFF;
GO
