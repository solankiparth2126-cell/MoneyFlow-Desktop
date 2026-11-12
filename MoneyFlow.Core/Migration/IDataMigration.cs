using System;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Core.Migration;

public interface IDataMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    string Description { get; }

    Task MigrateAsync(CompanyData data, CancellationToken cancellationToken);
}

public class MigrationProgressInfo
{
    public string CompanyId { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public int FromVersion { get; set; }
    public int TargetVersion { get; set; }
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public double ProgressPercentage { get; set; }
}

public interface IMigrationManager
{
    int CurrentDataVersion { get; }
    Task<bool> NeedsMigrationAsync(string filePath, CancellationToken ct = default);
    Task MigrateCompanyFileAsync(string filePath, string password, IProgress<MigrationProgressInfo>? progress = null, CancellationToken ct = default);
}
