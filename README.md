# MoneyFlow Desktop Accounting (MoneyFlow Desktop ERP)

MoneyFlow Desktop Accounting is a professional standalone Windows desktop accounting application built with C#, .NET 8, Windows Forms, SQL Server Express, and Entity Framework Core.

## Principles & Design Goals
- **Pure Double-Entry Accounting**: Total Debit = Total Credit enforced strictly. Unbalanced vouchers are rejected.
- **Standalone Offline Desktop**: Designed for a single Windows PC with a local SQL Server Express database. No cloud, no web server, no external dependencies.
- **Multi-Company Support**: Complete company data isolation within a single database (`MoneyFlowDB`).
- **Tally-Inspired Productivity**: Gateway of Accounting interface optimized for fast, keyboard-first navigation (F2, F4, F5, F6, F7, F8, F9, Esc).
- **Zero GST**: Pure, clean accounting focused on fundamental principles.

## Solution Structure
- `MoneyFlow.Desktop`: Windows Forms presentation layer, keyboard handlers, Gateway UI, and DI host.
- `MoneyFlow.Core`: Pure domain entities, DTOs, enums, constants, exceptions, and interfaces.
- `MoneyFlow.Data`: EF Core DbContext (`AppDbContext`), configurations, migrations, and seed data.
- `MoneyFlow.Services`: Independent accounting engine, voucher service, reporting, backup, and validation.
- `MoneyFlow.Reports`: RDLC report definitions and print/export rendering.
- `MoneyFlow.Tests`: Automated unit tests for double-entry principles, voucher validations, and balances.

## Requirements
- Windows 10 / Windows 11
- .NET 8 Desktop Runtime / SDK
- Microsoft SQL Server Express (2019/2022/2025) or LocalDB
