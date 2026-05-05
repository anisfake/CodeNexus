using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIUsageLogs.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.AIUsageLogs.Queries.GetAIProfitOverview;

public record GetAIProfitOverviewQuery : IRequest<Result<AIProfitOverviewResponse>>
{
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
}

