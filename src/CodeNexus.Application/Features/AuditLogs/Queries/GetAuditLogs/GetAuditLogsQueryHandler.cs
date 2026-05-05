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

    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    public async Task<Result<PaginationDto<AuditLogResponse>>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone);
        var fromDate = request.FromDate ?? TimeZoneInfo.ConvertTimeToUtc(nowVn.AddDays(-3), VietnamTimeZone);
        var toDate = request.ToDate ?? TimeZoneInfo.ConvertTimeToUtc(nowVn, VietnamTimeZone);

        var query = _context.AuditLogs
            .Include(a => a.User)
            .AsNoTracking()
            .Where(a => a.Timestamp >= fromDate && a.Timestamp <= toDate)
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
