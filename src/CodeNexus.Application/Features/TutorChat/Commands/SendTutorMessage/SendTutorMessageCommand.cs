using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TutorChat.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.TutorChat.Commands.SendTutorMessage;

public record SendTutorMessageCommand(
    Guid? ConversationId,
    Guid? LearningPathId,
    Guid? ChapterId,
    Guid? LessonId,
    string Message) : IRequest<Result<TutorChatResponseDto>>;
