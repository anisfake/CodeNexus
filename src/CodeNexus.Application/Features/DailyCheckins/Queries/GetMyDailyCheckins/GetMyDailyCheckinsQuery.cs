using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DailyCheckin.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.DailyCheckin.Queries.GetMyDailyCheckins;

public record GetMyDailyCheckinsQuery(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<List<DailyCheckinDto>>>;
