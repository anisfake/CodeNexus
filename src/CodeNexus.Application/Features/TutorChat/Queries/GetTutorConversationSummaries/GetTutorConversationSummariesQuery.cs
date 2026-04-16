using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TutorChat.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.TutorChat.Queries.GetTutorConversationSummaries;

public record GetTutorConversationSummariesQuery(
    Guid ConversationId,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<Result<PaginationDto<TutorConversationSummaryDto>>>;
