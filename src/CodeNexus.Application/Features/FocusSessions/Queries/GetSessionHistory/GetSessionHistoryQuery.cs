using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.FocusSessions.Queries.GetSessionHistory;

public record GetSessionHistoryQuery(
    Guid? TaskId = null,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<Result<List<FocusSessionDto>>>;