using System;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Core.Interfaces;

public interface ICompanyStorageEngine
{
    string GetDataRootDirectory();
    string GetCompaniesRootDirectory();
    string GetCompanyDirectory(string companyId);
    string GetCompanyFilePath(string companyId);
    string GetSystemDirectory();
    string GetBackupsDirectory(string? companyId = null);

    bool IsCompanyLocked(string companyId);
    IDisposable AcquireLock(string companyId);

    Task<CompanyFileHeader> ReadHeaderAsync(string filePath, CancellationToken ct = default);
    Task<(CompanyData Data, byte[] Dek)> LoadCompanyDataAsync(string filePath, string password, CancellationToken ct = default);
    Task SaveCompanyDataAsync(string filePath, CompanyData data, byte[] dek, string? updatePassword = null, CancellationToken ct = default);
    Task CreateInitialCompanyFileAsync(string filePath, CompanyData initialData, string password, CancellationToken ct = default);

    Task<string> CreateBackupAsync(string companyId, string? customBackupDir = null, string? reason = null, CancellationToken ct = default);
    Task RestoreBackupAsync(string backupFilePath, string targetCompanyId, string password, CancellationToken ct = default);
}
