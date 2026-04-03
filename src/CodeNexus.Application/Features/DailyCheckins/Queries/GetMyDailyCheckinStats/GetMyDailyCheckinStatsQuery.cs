using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DailyCheckin.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.DailyCheckin.Queries.GetMyDailyCheckinStats;

public record GetMyDailyCheckinStatsQuery : IRequest<Result<DailyCheckinStatsDto>>;
