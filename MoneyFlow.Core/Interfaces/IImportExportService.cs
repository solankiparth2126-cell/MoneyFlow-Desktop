using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;

namespace MoneyFlow.Core.Interfaces;

public interface IImportExportService
{
    Task<string> GenerateTemplateCsvAsync(ImportEntityType entityType);

    Task<ImportPreviewResultDto> PreviewImportCsvAsync(
        int companyId,
        ImportEntityType entityType,
        string csvContent,
        DuplicateAction duplicateAction,
        CancellationToken ct = default);

    Task<ImportExecutionResultDto> ExecuteImportAsync(
        int companyId,
        ImportEntityType entityType,
        ImportPreviewResultDto preview,
        DuplicateAction duplicateAction,
        CancellationToken ct = default);

    Task<string> ExportDataAsync(
        int companyId,
        ExportOptionsDto options,
        CancellationToken ct = default);
}
