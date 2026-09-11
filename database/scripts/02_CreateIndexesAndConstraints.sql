-- =========================================================================
-- MoneyFlow Desktop ERP — Index Tuning & Performance Script
-- Master Prompt Section 50 ("PERFORMANCE") & Section 61
-- =========================================================================

USE [MoneyFlowDB];
GO

-- 1. Vouchers Indexes (Single-column & Compound)
CREATE NONCLUSTERED INDEX [IX_Vouchers_CompanyId] 
    ON [dbo].[Vouchers] ([CompanyId] ASC);

CREATE NONCLUSTERED INDEX [IX_Vouchers_FinancialYearId] 
    ON [dbo].[Vouchers] ([FinancialYearId] ASC);

CREATE NONCLUSTERED INDEX [IX_Vouchers_VoucherDate] 
    ON [dbo].[Vouchers] ([VoucherDate] ASC);

CREATE NONCLUSTERED INDEX [IX_Vouchers_VoucherNumber] 
    ON [dbo].[Vouchers] ([VoucherNumber] ASC);

CREATE NONCLUSTERED INDEX [IX_Vouchers_CompanyId_VoucherNumber] 
    ON [dbo].[Vouchers] ([CompanyId] ASC, [VoucherNumber] ASC);

CREATE NONCLUSTERED INDEX [IX_Vouchers_Company_FY_Date_IsDeleted] 
    ON [dbo].[Vouchers] ([CompanyId] ASC, [FinancialYearId] ASC, [VoucherDate] ASC, [IsDeleted] ASC)
    INCLUDE ([VoucherNumber], [VoucherTypeId], [TotalAmount]);

CREATE NONCLUSTERED INDEX [IX_Vouchers_Company_Date_IsDeleted] 
    ON [dbo].[Vouchers] ([CompanyId] ASC, [VoucherDate] ASC, [IsDeleted] ASC)
    INCLUDE ([VoucherNumber], [VoucherTypeId], [TotalAmount]);

CREATE NONCLUSTERED INDEX [IX_Vouchers_Company_Type_FY] 
    ON [dbo].[Vouchers] ([CompanyId] ASC, [VoucherTypeId] ASC, [FinancialYearId] ASC);
GO

-- 2. VoucherEntries Indexes
CREATE NONCLUSTERED INDEX [IX_VoucherEntries_VoucherId] 
    ON [dbo].[VoucherEntries] ([VoucherId] ASC);

CREATE NONCLUSTERED INDEX [IX_VoucherEntries_LedgerId] 
    ON [dbo].[VoucherEntries] ([LedgerId] ASC);

CREATE NONCLUSTERED INDEX [IX_VoucherEntries_LedgerId_VoucherId] 
    ON [dbo].[VoucherEntries] ([LedgerId] ASC, [VoucherId] ASC)
    INCLUDE ([Debit], [Credit]);
GO

-- 3. Groups Indexes
CREATE NONCLUSTERED INDEX [IX_Groups_CompanyId_GroupName] 
    ON [dbo].[Groups] ([CompanyId] ASC, [GroupName] ASC);

CREATE NONCLUSTERED INDEX [IX_Groups_ParentGroupId] 
    ON [dbo].[Groups] ([ParentGroupId] ASC);

CREATE NONCLUSTERED INDEX [IX_Groups_CompanyId_IsActive] 
    ON [dbo].[Groups] ([CompanyId] ASC, [IsActive] ASC);
GO

-- 4. Ledgers Indexes
CREATE NONCLUSTERED INDEX [IX_Ledgers_CompanyId_LedgerName] 
    ON [dbo].[Ledgers] ([CompanyId] ASC, [LedgerName] ASC);

CREATE NONCLUSTERED INDEX [IX_Ledgers_GroupId] 
    ON [dbo].[Ledgers] ([GroupId] ASC);

CREATE NONCLUSTERED INDEX [IX_Ledgers_CompanyId_IsActive] 
    ON [dbo].[Ledgers] ([CompanyId] ASC, [IsActive] ASC);
GO

-- 5. StockItems & Units Indexes
CREATE UNIQUE NONCLUSTERED INDEX [IX_StockItems_CompanyId_ItemName] 
    ON [dbo].[StockItems] ([CompanyId] ASC, [ItemName] ASC);

CREATE NONCLUSTERED INDEX [IX_StockItems_CompanyId_IsActive] 
    ON [dbo].[StockItems] ([CompanyId] ASC, [IsActive] ASC);

CREATE UNIQUE NONCLUSTERED INDEX [IX_Units_CompanyId_Symbol] 
    ON [dbo].[Units] ([CompanyId] ASC, [Symbol] ASC);
GO

-- 6. AuditLogs Indexes
CREATE NONCLUSTERED INDEX [IX_AuditLogs_Timestamp] 
    ON [dbo].[AuditLogs] ([Timestamp] DESC);

CREATE NONCLUSTERED INDEX [IX_AuditLogs_CompanyId_Timestamp] 
    ON [dbo].[AuditLogs] ([CompanyId] ASC, [Timestamp] DESC);

CREATE NONCLUSTERED INDEX [IX_AuditLogs_Module_Action] 
    ON [dbo].[AuditLogs] ([Module] ASC, [Action] ASC);
GO
