-- =========================================================================
-- MoneyFlow Desktop ERP — Database Creation & Relational Schema
-- Master Prompt Section 61 ("DATABASE MANAGEMENT")
-- Target DBMS: Microsoft SQL Server 2019 / 2022 / Express
-- Database Name: MoneyFlowDB
-- =========================================================================

USE [master];
GO

-- 1. Create Database if not exists
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'MoneyFlowDB')
BEGIN
    CREATE DATABASE [MoneyFlowDB]
    COLLATE Latin1_General_CI_AS;
END
GO

USE [MoneyFlowDB];
GO

-- 2. Companies Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Companies]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Companies] (
        [CompanyId] INT IDENTITY(1,1) NOT NULL,
        [CompanyName] NVARCHAR(200) NOT NULL,
        [MailingName] NVARCHAR(200) NULL,
        [Address] NVARCHAR(500) NULL,
        [Country] NVARCHAR(100) NULL,
        [State] NVARCHAR(100) NULL,
        [PinCode] NVARCHAR(20) NULL,
        [Telephone] NVARCHAR(50) NULL,
        [Mobile] NVARCHAR(50) NULL,
        [Email] NVARCHAR(100) NULL,
        [CurrencySymbol] NVARCHAR(10) NOT NULL DEFAULT (N'₹'),
        [FormalName] NVARCHAR(50) NOT NULL DEFAULT (N'INR'),
        [DecimalPlaces] INT NOT NULL DEFAULT (2),
        [IsActive] BIT NOT NULL DEFAULT (1),
        [CreatedAt] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY CLUSTERED ([CompanyId] ASC)
    );
END
GO

-- 3. FinancialYears Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FinancialYears]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[FinancialYears] (
        [FinancialYearId] INT IDENTITY(1,1) NOT NULL,
        [CompanyId] INT NOT NULL,
        [YearName] NVARCHAR(50) NOT NULL,
        [StartDate] DATE NOT NULL,
        [EndDate] DATE NOT NULL,
        [IsClosed] BIT NOT NULL DEFAULT (0),
        [CreatedAt] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_FinancialYears] PRIMARY KEY CLUSTERED ([FinancialYearId] ASC),
        CONSTRAINT [FK_FinancialYears_Companies] FOREIGN KEY ([CompanyId]) 
            REFERENCES [dbo].[Companies] ([CompanyId]) ON DELETE RESTRICT
    );
END
GO

-- 4. Groups Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Groups]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Groups] (
        [GroupId] INT IDENTITY(1,1) NOT NULL,
        [CompanyId] INT NOT NULL,
        [GroupName] NVARCHAR(150) NOT NULL,
        [ParentGroupId] INT NULL,
        [Nature] NVARCHAR(50) NOT NULL,
        [IsPrimary] BIT NOT NULL DEFAULT (0),
        [IsActive] BIT NOT NULL DEFAULT (1),
        [CreatedAt] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Groups] PRIMARY KEY CLUSTERED ([GroupId] ASC),
        CONSTRAINT [FK_Groups_Companies] FOREIGN KEY ([CompanyId]) 
            REFERENCES [dbo].[Companies] ([CompanyId]) ON DELETE RESTRICT,
        CONSTRAINT [FK_Groups_ParentGroup] FOREIGN KEY ([ParentGroupId]) 
            REFERENCES [dbo].[Groups] ([GroupId]) ON DELETE NO ACTION
    );
END
GO

-- 5. Ledgers Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Ledgers]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Ledgers] (
        [LedgerId] INT IDENTITY(1,1) NOT NULL,
        [CompanyId] INT NOT NULL,
        [GroupId] INT NOT NULL,
        [LedgerName] NVARCHAR(150) NOT NULL,
        [OpeningBalance] DECIMAL(18,2) NOT NULL DEFAULT (0.00),
        [OpeningBalanceType] NVARCHAR(10) NOT NULL DEFAULT (N'Dr'),
        [ClosingBalance] DECIMAL(18,2) NOT NULL DEFAULT (0.00),
        [ClosingBalanceType] NVARCHAR(10) NOT NULL DEFAULT (N'Dr'),
        [Address] NVARCHAR(500) NULL,
        [ContactPerson] NVARCHAR(100) NULL,
        [Phone] NVARCHAR(50) NULL,
        [Email] NVARCHAR(100) NULL,
        [IsActive] BIT NOT NULL DEFAULT (1),
        [CreatedAt] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Ledgers] PRIMARY KEY CLUSTERED ([LedgerId] ASC),
        CONSTRAINT [FK_Ledgers_Companies] FOREIGN KEY ([CompanyId]) 
            REFERENCES [dbo].[Companies] ([CompanyId]) ON DELETE RESTRICT,
        CONSTRAINT [FK_Ledgers_Groups] FOREIGN KEY ([GroupId]) 
            REFERENCES [dbo].[Groups] ([GroupId]) ON DELETE RESTRICT
    );
END
GO

-- 6. VoucherTypes Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[VoucherTypes]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[VoucherTypes] (
        [VoucherTypeId] INT IDENTITY(1,1) NOT NULL,
        [Name] NVARCHAR(50) NOT NULL,
        [Code] NVARCHAR(10) NOT NULL,
        [Type] INT NOT NULL,
        [Prefix] NVARCHAR(10) NULL,
        [IsActive] BIT NOT NULL DEFAULT (1),
        CONSTRAINT [PK_VoucherTypes] PRIMARY KEY CLUSTERED ([VoucherTypeId] ASC)
    );
END
GO

-- 7. Vouchers Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Vouchers]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Vouchers] (
        [VoucherId] INT IDENTITY(1,1) NOT NULL,
        [CompanyId] INT NOT NULL,
        [FinancialYearId] INT NOT NULL,
        [VoucherTypeId] INT NOT NULL,
        [VoucherNumber] NVARCHAR(50) NOT NULL,
        [VoucherDate] DATE NOT NULL,
        [ReferenceNumber] NVARCHAR(100) NULL,
        [Narration] NVARCHAR(1000) NULL,
        [TotalAmount] DECIMAL(18,2) NOT NULL DEFAULT (0.00),
        [IsDeleted] BIT NOT NULL DEFAULT (0),
        [CreatedAt] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Vouchers] PRIMARY KEY CLUSTERED ([VoucherId] ASC),
        CONSTRAINT [FK_Vouchers_Companies] FOREIGN KEY ([CompanyId]) 
            REFERENCES [dbo].[Companies] ([CompanyId]) ON DELETE RESTRICT,
        CONSTRAINT [FK_Vouchers_FinancialYears] FOREIGN KEY ([FinancialYearId]) 
            REFERENCES [dbo].[FinancialYears] ([FinancialYearId]) ON DELETE RESTRICT,
        CONSTRAINT [FK_Vouchers_VoucherTypes] FOREIGN KEY ([VoucherTypeId]) 
            REFERENCES [dbo].[VoucherTypes] ([VoucherTypeId]) ON DELETE RESTRICT
    );
END
GO

-- 8. VoucherEntries Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[VoucherEntries]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[VoucherEntries] (
        [VoucherEntryId] INT IDENTITY(1,1) NOT NULL,
        [VoucherId] INT NOT NULL,
        [LedgerId] INT NOT NULL,
        [Debit] DECIMAL(18,2) NOT NULL DEFAULT (0.00),
        [Credit] DECIMAL(18,2) NOT NULL DEFAULT (0.00),
        [Narration] NVARCHAR(500) NULL,
        CONSTRAINT [PK_VoucherEntries] PRIMARY KEY CLUSTERED ([VoucherEntryId] ASC),
        CONSTRAINT [FK_VoucherEntries_Vouchers] FOREIGN KEY ([VoucherId]) 
            REFERENCES [dbo].[Vouchers] ([VoucherId]) ON DELETE CASCADE,
        CONSTRAINT [FK_VoucherEntries_Ledgers] FOREIGN KEY ([LedgerId]) 
            REFERENCES [dbo].[Ledgers] ([LedgerId]) ON DELETE RESTRICT
    );
END
GO

-- 9. Units Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Units]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Units] (
        [UnitId] INT IDENTITY(1,1) NOT NULL,
        [CompanyId] INT NOT NULL,
        [UnitName] NVARCHAR(50) NOT NULL,
        [Symbol] NVARCHAR(15) NOT NULL,
        [DecimalPlaces] INT NOT NULL DEFAULT (0),
        [IsActive] BIT NOT NULL DEFAULT (1),
        [CreatedAt] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Units] PRIMARY KEY CLUSTERED ([UnitId] ASC),
        CONSTRAINT [FK_Units_Companies] FOREIGN KEY ([CompanyId]) 
            REFERENCES [dbo].[Companies] ([CompanyId]) ON DELETE RESTRICT
    );
END
GO

-- 10. StockItems Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[StockItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[StockItems] (
        [StockItemId] INT IDENTITY(1,1) NOT NULL,
        [CompanyId] INT NOT NULL,
        [ItemName] NVARCHAR(150) NOT NULL,
        [UnitId] INT NULL,
        [OpeningQuantity] DECIMAL(18,4) NOT NULL DEFAULT (0.0000),
        [OpeningRate] DECIMAL(18,2) NOT NULL DEFAULT (0.00),
        [OpeningValue] DECIMAL(18,2) NOT NULL DEFAULT (0.00),
        [ClosingQuantity] DECIMAL(18,4) NOT NULL DEFAULT (0.0000),
        [ClosingRate] DECIMAL(18,2) NOT NULL DEFAULT (0.00),
        [ClosingValue] DECIMAL(18,2) NOT NULL DEFAULT (0.00),
        [IsActive] BIT NOT NULL DEFAULT (1),
        [CreatedAt] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_StockItems] PRIMARY KEY CLUSTERED ([StockItemId] ASC),
        CONSTRAINT [FK_StockItems_Companies] FOREIGN KEY ([CompanyId]) 
            REFERENCES [dbo].[Companies] ([CompanyId]) ON DELETE RESTRICT,
        CONSTRAINT [FK_StockItems_Units] FOREIGN KEY ([UnitId]) 
            REFERENCES [dbo].[Units] ([UnitId]) ON DELETE SET NULL
    );
END
GO

-- 11. Users & Roles Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Roles]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Roles] (
        [RoleId] INT IDENTITY(1,1) NOT NULL,
        [RoleName] NVARCHAR(50) NOT NULL,
        [Description] NVARCHAR(250) NULL,
        [IsSystemRole] BIT NOT NULL DEFAULT (1),
        CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([RoleId] ASC)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Users] (
        [UserId] INT IDENTITY(1,1) NOT NULL,
        [Username] NVARCHAR(50) NOT NULL,
        [PasswordHash] NVARCHAR(500) NOT NULL,
        [PasswordSalt] NVARCHAR(250) NOT NULL,
        [DisplayName] NVARCHAR(100) NOT NULL,
        [RoleId] INT NOT NULL,
        [IsActive] BIT NOT NULL DEFAULT (1),
        [CreatedAt] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [LastLoginAt] DATETIME2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED ([UserId] ASC),
        CONSTRAINT [FK_Users_Roles] FOREIGN KEY ([RoleId]) 
            REFERENCES [dbo].[Roles] ([RoleId]) ON DELETE RESTRICT
    );
END
GO

-- 12. Settings Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Settings]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Settings] (
        [SettingKey] NVARCHAR(100) NOT NULL,
        [SettingValue] NVARCHAR(MAX) NULL,
        [Description] NVARCHAR(250) NULL,
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Settings] PRIMARY KEY CLUSTERED ([SettingKey] ASC)
    );
END
GO

-- 13. AuditLogs Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AuditLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AuditLogs] (
        [AuditLogId] INT IDENTITY(1,1) NOT NULL,
        [CompanyId] INT NULL,
        [UserId] INT NULL,
        [Username] NVARCHAR(50) NOT NULL,
        [Action] NVARCHAR(50) NOT NULL,
        [Module] NVARCHAR(50) NOT NULL,
        [EntityName] NVARCHAR(100) NULL,
        [EntityId] NVARCHAR(50) NULL,
        [Details] NVARCHAR(MAX) NULL,
        [Timestamp] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY CLUSTERED ([AuditLogId] ASC)
    );
END
GO
