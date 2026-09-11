using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;

namespace MoneyFlow.Services.Security;

public class AuditService : IAuditService
{
    private readonly AppDbContext _context;
    private readonly IUserContext _userContext;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        AppDbContext context,
        IUserContext userContext,
        ILogger<AuditService> logger)
    {
        _context = context;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task LogAsync(
        int? companyId,
        string action,
        string module,
        string recordId,
        string description,
        CancellationToken ct = default)
    {
        try
        {
            var auditEntry = new AuditLog
            {
                CompanyId = companyId,
                Username = _userContext.Username ?? "system",
                Action = action,
                Module = module,
                RecordId = recordId ?? string.Empty,
                Timestamp = DateTime.Now,
                Description = description ?? string.Empty
            };

            _context.AuditLogs.Add(auditEntry);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed writing audit log for Action={Action}, Module={Module}", action, module);
        }
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetLogsAsync(AuditLogFilterDto filter, CancellationToken ct = default)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (filter.CompanyId.HasValue)
        {
            query = query.Where(a => a.CompanyId == filter.CompanyId || a.CompanyId == null);
        }

        if (!string.IsNullOrWhiteSpace(filter.Module))
        {
            query = query.Where(a => a.Module == filter.Module);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(a => a.Action == filter.Action);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= filter.ToDate.Value);
        }

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Take(filter.MaxRecords > 0 ? filter.MaxRecords : 200)
            .Select(a => new AuditLogDto
            {
                AuditLogId = a.AuditLogId,
                CompanyId = a.CompanyId,
                Username = a.Username,
                Action = a.Action,
                Module = a.Module,
                RecordId = a.RecordId,
                Timestamp = a.Timestamp,
                Description = a.Description
            })
            .ToListAsync(ct);
    }
}
