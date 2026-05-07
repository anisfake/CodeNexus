using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Payments.Queries.GetMonthlyFinanceOverview;

public record GetMonthlyFinanceOverviewQuery(
    int? Year,
    int? Month) : IRequest<Result<MonthlyFinanceOverviewResponse>>;
