using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DailyCheckin.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.DailyCheckin.Queries.GetDailyCheckinBySessionId;

public record GetDailyCheckinBySessionIdQuery(Guid SessionId) : IRequest<Result<DailyCheckinDto>>;
