using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AuditLogs.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.AuditLogs.Queries.GetAuditLogs;

public record GetAuditLogsQuery : IRequest<Result<PaginationDto<AuditLogResponse>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Action { get; init; }
    public string? TableName { get; init; }
    public Guid? UserId { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public AuditLogSortBy SortBy { get; init; } = AuditLogSortBy.Timestamp;
    public bool SortDescending { get; init; } = true;
}

public enum AuditLogSortBy
{
    Timestamp,
    Action,
    TableName
}
