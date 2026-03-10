using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.FocusSessions.Queries.GetActiveSession;

public record GetActiveSessionQuery(Guid TaskId) : IRequest<Result<ActiveSessionDto?>>;