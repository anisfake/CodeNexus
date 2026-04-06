using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TutorChat.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.TutorChat.Queries.GetTutorConversationMessages;

public record GetTutorConversationMessagesQuery(
    Guid ConversationId,
    int PageNumber = 1,
    int PageSize = 30) : IRequest<Result<TutorMessagesPageDto>>;
