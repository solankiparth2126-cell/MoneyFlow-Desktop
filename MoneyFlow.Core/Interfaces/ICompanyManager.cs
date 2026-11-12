using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Core.Interfaces;

public class CompanyFileValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public CompanyFileHeader? Header { get; set; }
    public bool RequiresMigration { get; set; }
    public bool IsUnsupportedNewerVersion { get; set; }
}

public interface ICompanyManager
{
    Task<CompanySummaryDto> CreateCompanyAsync(CompanyCreateDto dto, string password, CancellationToken ct = default);
    Task<bool> OpenCompanyAsync(string companyId, string password, CancellationToken ct = default);
    void CloseCompany();
    Task<bool> DeleteCompanyAsync(string companyId, bool createSafetyBackup = true, CancellationToken ct = default);
    Task<bool> RenameCompanyAsync(string companyId, string newName, CancellationToken ct = default);
    Task<IReadOnlyList<CompanySummaryDto>> GetCompaniesAsync(CancellationToken ct = default);
    Task<CompanySummaryDto> ImportCompanyFileAsync(string sourceCompanyDataFilePath, string? targetCompanyId = null, CancellationToken ct = default);
    Task<string> ExportCompanyFileAsync(string companyId, string targetDirectory, CancellationToken ct = default);
    Task<bool> VerifyPasswordAsync(string companyId, string password, CancellationToken ct = default);
    CompanyFileValidationResult ValidateCompanyFile(string filePath);
    bool CompanyExists(string companyId);
}
