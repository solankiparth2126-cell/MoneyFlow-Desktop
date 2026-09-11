using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;

namespace MoneyFlow.Core.Interfaces;

public interface IBackupRestoreService
{
    string GetDefaultBackupDirectory();

    Task<BackupFileInfo> CreateBackupAsync(BackupCreateOptionsDto options, CancellationToken ct = default);

    Task<BackupManifestDto?> ReadManifestAsync(string backupFilePath, CancellationToken ct = default);

    Task<RestoreResultDto> RestoreBackupAsync(RestoreOptionsDto options, CancellationToken ct = default);

    Task<IReadOnlyList<BackupFileInfo>> GetBackupHistoryAsync(string? directory = null, CancellationToken ct = default);
}
