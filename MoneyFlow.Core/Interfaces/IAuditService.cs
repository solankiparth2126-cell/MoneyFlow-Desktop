using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;

namespace MoneyFlow.Core.Interfaces;

public interface IAuditService
{
    Task LogAsync(int? companyId, string action, string module, string recordId, string description, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogDto>> GetLogsAsync(AuditLogFilterDto filter, CancellationToken ct = default);
}
