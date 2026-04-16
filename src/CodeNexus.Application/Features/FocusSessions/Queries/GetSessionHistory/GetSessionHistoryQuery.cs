using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.FocusSessions.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.FocusSessions.Queries.GetSessionHistory;

public record GetSessionHistoryQuery(
    Guid? TaskId = null,
    SessionStatus? SessionStatus = null,
    SessionType? SessionType = null,
    DateTime? StartedFrom = null,
    DateTime? StartedTo = null,
    bool IncludeAbandoned = false,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<Result<PaginationDto<FocusSessionHistoryItemDto>>>;
