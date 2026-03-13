using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AuditLogs.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.AuditLogs.Queries.GetAuditLogs;

public class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, Result<PaginationDto<AuditLogResponse>>>
{
    private readonly IApplicationDbContext _context;

    public GetAuditLogsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginationDto<AuditLogResponse>>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.AuditLogs
            .Include(a => a.User)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.ToLower();
            query = query.Where(a => a.Action.ToLower().Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(request.TableName))
        {
            var tableName = request.TableName.ToLower();
            query = query.Where(a => a.TableName != null && a.TableName.ToLower().Contains(tableName));
        }

        if (request.UserId.HasValue)
        {
            query = query.Where(a => a.UserId == request.UserId.Value);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= request.ToDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = request.SortBy switch
        {
            AuditLogSortBy.Action => request.SortDescending
                ? query.OrderByDescending(a => a.Action)
                : query.OrderBy(a => a.Action),
            AuditLogSortBy.TableName => request.SortDescending
                ? query.OrderByDescending(a => a.TableName)
                : query.OrderBy(a => a.TableName),
            _ => request.SortDescending
                ? query.OrderByDescending(a => a.Timestamp)
                : query.OrderBy(a => a.Timestamp)
        };

        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AuditLogResponse(
                a.LogId,
                a.UserId,
                a.User != null ? a.User.Username : null,
                a.Action,
                a.TableName,
                a.RecordId,
                a.OldValue,
                a.NewValue,
                a.Timestamp,
                a.IPAddress
            ))
            .ToListAsync(cancellationToken);

        var paginationResult = new PaginationDto<AuditLogResponse>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };

        return Result<PaginationDto<AuditLogResponse>>.Success(paginationResult);
    }
}
