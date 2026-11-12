using System;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Core.Migration.Migrations;

public class MigrationV1ToV2 : IDataMigration
{
    public int FromVersion => 1;
    public int ToVersion => 2;
    public string Description => "Contact, phone, and email standardization across ledgers and company records.";

    public Task MigrateAsync(CompanyData data, CancellationToken cancellationToken)
    {
        data.Company.Email ??= string.Empty;
        data.Company.Phone ??= string.Empty;
        data.Company.Address ??= string.Empty;

        foreach (var ledger in data.Ledgers)
        {
            ledger.Email ??= string.Empty;
            ledger.Phone ??= string.Empty;
            ledger.Address ??= string.Empty;
        }

        return Task.CompletedTask;
    }
}

public class MigrationV2ToV3 : IDataMigration
{
    public int FromVersion => 2;
    public int ToVersion => 3;
    public string Description => "Credit limit, credit days, and PAN field normalization.";

    public Task MigrateAsync(CompanyData data, CancellationToken cancellationToken)
    {
        data.Company.PAN ??= string.Empty;

        foreach (var ledger in data.Ledgers)
        {
            if (ledger.CreditDays < 0) ledger.CreditDays = 0;
            if (ledger.CreditLimit < 0) ledger.CreditLimit = 0;
            ledger.PAN ??= string.Empty;
            ledger.State ??= string.Empty;
        }

        return Task.CompletedTask;
    }
}

public class MigrationV3ToV4 : IDataMigration
{
    public int FromVersion => 3;
    public int ToVersion => 4;
    public string Description => "Voucher narration, reference numbering, and entry integrity validation.";

    public Task MigrateAsync(CompanyData data, CancellationToken cancellationToken)
    {
        foreach (var voucher in data.Vouchers)
        {
            voucher.Narration ??= string.Empty;
            voucher.ReferenceNumber ??= string.Empty;
        }

        foreach (var entry in data.VoucherEntries)
        {
            entry.Narration ??= string.Empty;
        }

        return Task.CompletedTask;
    }
}

public class MigrationV4ToV5 : IDataMigration
{
    public int FromVersion => 4;
    public int ToVersion => 5;
    public string Description => "Audit log tracking enhancements and security role mapping normalization.";

    public Task MigrateAsync(CompanyData data, CancellationToken cancellationToken)
    {
        foreach (var log in data.AuditLogs)
        {
            log.Description ??= string.Empty;
            log.Action ??= "SYSTEM";
        }

        foreach (var role in data.Roles)
        {
            role.RoleName ??= "User";
        }

        return Task.CompletedTask;
    }
}

public class MigrationV5ToV6 : IDataMigration
{
    public int FromVersion => 5;
    public int ToVersion => 6;
    public string Description => "Inventory unit precision and schema version 6 compliance.";

    public Task MigrateAsync(CompanyData data, CancellationToken cancellationToken)
    {
        foreach (var unit in data.Units)
        {
            if (unit.DecimalPlaces < 0) unit.DecimalPlaces = 0;
            if (unit.DecimalPlaces > 4) unit.DecimalPlaces = 4;
        }

        foreach (var item in data.StockItems)
        {
            item.ItemName ??= string.Empty;
            if (item.OpeningQuantity < 0) item.OpeningQuantity = 0;
        }

        return Task.CompletedTask;
    }
}
