using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Services.Security;

public class AuditService : IAuditService
{
    private readonly IRepository<AuditLog> _auditRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserContext _userContext;
    private readonly ILogger<AuditService> _logger;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public AuditService(
        IRepository<AuditLog> auditRepo,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        ILogger<AuditService> logger)
    {
        _auditRepo = auditRepo;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
        _logger = logger;
    }

    public AuditService(
        IRepository<AuditLog> auditRepo,
        IUnitOfWork unitOfWork,
        IUserContext userContext)
        : this(auditRepo, unitOfWork, userContext, Microsoft.Extensions.Logging.Abstractions.NullLogger<AuditService>.Instance)
    {
    }

    internal AuditService(
        MoneyFlow.Data.AppDbContext context,
        IUserContext userContext,
        ILogger<AuditService> logger)
        : this(
            new MoneyFlow.Data.Repositories.Repository<AuditLog>(context),
            new MoneyFlow.Data.Repositories.UnitOfWork(context),
            userContext,
            logger)
    {
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
                Timestamp = DateTime.UtcNow,
                Description = description ?? string.Empty
            };

            await _auditRepo.AddAsync(auditEntry, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed writing audit log for Action={Action}, Module={Module}", action, module);
        }
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetLogsAsync(AuditLogFilterDto filter, CancellationToken ct = default)
    {
        var logs = await _auditRepo.GetAllAsync(ct);
        IEnumerable<AuditLog> query = logs;

        if (filter.CompanyId.HasValue)
        {
            query = query.Where(a => a.CompanyId == filter.CompanyId || a.CompanyId == null);
        }

        if (!string.IsNullOrWhiteSpace(filter.Module))
        {
            query = query.Where(a => a.Module.Equals(filter.Module, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(a => a.Action.Equals(filter.Action, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= filter.ToDate.Value);
        }

        return query
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
            .ToList();
    }
}
