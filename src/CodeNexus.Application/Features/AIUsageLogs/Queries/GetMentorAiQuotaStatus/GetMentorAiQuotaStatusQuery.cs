using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIUsageLogs.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.AIUsageLogs.Queries.GetMentorAiQuotaStatus;

public record GetMentorAiQuotaStatusQuery : IRequest<Result<PaginationDto<MentorAiQuotaStatusResponse>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int NearThresholdPercent { get; init; } = 80;
    public bool OnlyNearOrReached { get; init; } = true;
    public string? Search { get; init; }
}

