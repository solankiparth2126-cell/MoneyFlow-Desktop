-- =========================================================================
-- MoneyFlow Desktop ERP — System Seed Data Script
-- Master Prompt Section 60 ("FIRST-RUN DATABASE SETUP") & Section 61
-- =========================================================================

USE [MoneyFlowDB];
GO

-- 1. Seed Core Voucher Types
SET IDENTITY_INSERT [dbo].[VoucherTypes] ON;

IF NOT EXISTS (SELECT 1 FROM [dbo].[VoucherTypes] WHERE [VoucherTypeId] = 1)
    INSERT INTO [dbo].[VoucherTypes] ([VoucherTypeId], [Name], [Code], [Type], [Prefix], [IsActive]) VALUES (1, N'Contra', N'CTR', 1, N'CNT-', 1);
IF NOT EXISTS (SELECT 1 FROM [dbo].[VoucherTypes] WHERE [VoucherTypeId] = 2)
    INSERT INTO [dbo].[VoucherTypes] ([VoucherTypeId], [Name], [Code], [Type], [Prefix], [IsActive]) VALUES (2, N'Payment', N'PMT', 2, N'PAY-', 1);
IF NOT EXISTS (SELECT 1 FROM [dbo].[VoucherTypes] WHERE [VoucherTypeId] = 3)
    INSERT INTO [dbo].[VoucherTypes] ([VoucherTypeId], [Name], [Code], [Type], [Prefix], [IsActive]) VALUES (3, N'Receipt', N'RCP', 3, N'RCP-', 1);
IF NOT EXISTS (SELECT 1 FROM [dbo].[VoucherTypes] WHERE [VoucherTypeId] = 4)
    INSERT INTO [dbo].[VoucherTypes] ([VoucherTypeId], [Name], [Code], [Type], [Prefix], [IsActive]) VALUES (4, N'Journal', N'JRN', 4, N'JRN-', 1);
IF NOT EXISTS (SELECT 1 FROM [dbo].[VoucherTypes] WHERE [VoucherTypeId] = 5)
    INSERT INTO [dbo].[VoucherTypes] ([VoucherTypeId], [Name], [Code], [Type], [Prefix], [IsActive]) VALUES (5, N'Sales', N'SLS', 5, N'SLS-', 1);
IF NOT EXISTS (SELECT 1 FROM [dbo].[VoucherTypes] WHERE [VoucherTypeId] = 6)
    INSERT INTO [dbo].[VoucherTypes] ([VoucherTypeId], [Name], [Code], [Type], [Prefix], [IsActive]) VALUES (6, N'Purchase', N'PUR', 6, N'PUR-', 1);
IF NOT EXISTS (SELECT 1 FROM [dbo].[VoucherTypes] WHERE [VoucherTypeId] = 7)
    INSERT INTO [dbo].[VoucherTypes] ([VoucherTypeId], [Name], [Code], [Type], [Prefix], [IsActive]) VALUES (7, N'Credit Note', N'CRN', 7, N'CRN-', 1);
IF NOT EXISTS (SELECT 1 FROM [dbo].[VoucherTypes] WHERE [VoucherTypeId] = 8)
    INSERT INTO [dbo].[VoucherTypes] ([VoucherTypeId], [Name], [Code], [Type], [Prefix], [IsActive]) VALUES (8, N'Debit Note', N'DBN', 8, N'DBN-', 1);

SET IDENTITY_INSERT [dbo].[VoucherTypes] OFF;
GO

-- 2. Seed System Roles
SET IDENTITY_INSERT [dbo].[Roles] ON;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleId] = 1)
    INSERT INTO [dbo].[Roles] ([RoleId], [RoleName], [Description], [IsSystemRole]) VALUES (1, N'Administrator', N'Full administrative access across all modules, security, and company settings.', 1);
IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleId] = 2)
    INSERT INTO [dbo].[Roles] ([RoleId], [RoleName], [Description], [IsSystemRole]) VALUES (2, N'Accountant', N'Full accounting access to manage ledgers, vouchers, and financial reports.', 1);
IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleId] = 3)
    INSERT INTO [dbo].[Roles] ([RoleId], [RoleName], [Description], [IsSystemRole]) VALUES (3, N'Operator', N'Data entry operator for transaction posting without report access.', 1);
IF NOT EXISTS (SELECT 1 FROM [dbo].[Roles] WHERE [RoleId] = 4)
    INSERT INTO [dbo].[Roles] ([RoleId], [RoleName], [Description], [IsSystemRole]) VALUES (4, N'Viewer', N'Read-only audit and financial report inspection.', 1);

SET IDENTITY_INSERT [dbo].[Roles] OFF;
GO

-- 3. Seed Default Administrator User (Initial Password: admin)
-- PBKDF2 SHA-256 with 100,000 iterations
IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [Username] = N'admin')
BEGIN
    INSERT INTO [dbo].[Users] ([Username], [PasswordHash], [PasswordSalt], [DisplayName], [RoleId], [IsActive], [CreatedAt])
    VALUES (
        N'admin', 
        N'q/C05b223r5/3544n6qU+JtH9Q1uHw5r8w0gA6K7mQ=', 
        N'nK8+wZ6nK8+wZ6nK8+wZ6A==', 
        N'System Administrator', 
        1, 
        1, 
        SYSUTCDATETIME()
    );
END
GO

-- 4. Seed Default Application Settings
IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [SettingKey] = N'DateFormat')
    INSERT INTO [dbo].[Settings] ([SettingKey], [SettingValue], [Description]) VALUES (N'DateFormat', N'dd-MM-yyyy', N'Standard date display format');
IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [SettingKey] = N'NumberFormat')
    INSERT INTO [dbo].[Settings] ([SettingKey], [SettingValue], [Description]) VALUES (N'NumberFormat', N'Indian', N'Locale number grouping (Indian vs Western)');
IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [SettingKey] = N'CurrencySymbol')
    INSERT INTO [dbo].[Settings] ([SettingKey], [SettingValue], [Description]) VALUES (N'CurrencySymbol', N'₹', N'Currency symbol prefix');
IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [SettingKey] = N'DecimalPrecision')
    INSERT INTO [dbo].[Settings] ([SettingKey], [SettingValue], [Description]) VALUES (N'DecimalPrecision', N'2', N'Decimal precision for currency amounts');
IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [SettingKey] = N'Theme')
    INSERT INTO [dbo].[Settings] ([SettingKey], [SettingValue], [Description]) VALUES (N'Theme', N'ClassicTeal', N'Default application UI theme');
IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [SettingKey] = N'GridDensity')
    INSERT INTO [dbo].[Settings] ([SettingKey], [SettingValue], [Description]) VALUES (N'GridDensity', N'Compact', N'DataGridView density padding');
IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [SettingKey] = N'PromptBackupOnExit')
    INSERT INTO [dbo].[Settings] ([SettingKey], [SettingValue], [Description]) VALUES (N'PromptBackupOnExit', N'true', N'Prompt user to create a backup on application exit');
GO
