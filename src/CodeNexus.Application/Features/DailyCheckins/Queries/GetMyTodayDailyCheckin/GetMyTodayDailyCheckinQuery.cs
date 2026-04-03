using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DailyCheckin.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.DailyCheckin.Queries.GetMyTodayDailyCheckin;

public record GetMyTodayDailyCheckinQuery : IRequest<Result<DailyCheckinDto>>;
