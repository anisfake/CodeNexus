using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIUsageLogs.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.AIUsageLogs.Queries.GetAIUsageSummary;

public record GetAIUsageSummaryQuery : IRequest<Result<List<AIUsageSummaryResponse>>>
{
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public bool IncludeProviderModelBreakdown { get; init; } = false;
}
