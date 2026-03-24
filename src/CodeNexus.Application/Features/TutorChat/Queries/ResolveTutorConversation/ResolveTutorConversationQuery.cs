using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TutorChat.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.TutorChat.Queries.ResolveTutorConversation;

public record ResolveTutorConversationQuery(
    Guid? LearningPathId,
    Guid? ChapterId,
    Guid? LessonId,
    bool CreateIfMissing) : IRequest<Result<ResolveTutorConversationResponseDto>>;
