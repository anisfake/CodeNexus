using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIUsageLogs.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.AIUsageLogs.Queries.GetAIUsageLogs;

public record GetAIUsageLogsQuery : IRequest<Result<PaginationDto<AIUsageLogResponse>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public AIUsageType? UsageType { get; init; }
    public string? ProviderName { get; init; }
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public AIUsageLogSortBy SortBy { get; init; } = AIUsageLogSortBy.CreatedAt;
    public bool SortDescending { get; init; } = true;
}

public enum AIUsageLogSortBy
{
    CreatedAt,
    TotalTokens,
    InputTokens,
    OutputTokens
}
