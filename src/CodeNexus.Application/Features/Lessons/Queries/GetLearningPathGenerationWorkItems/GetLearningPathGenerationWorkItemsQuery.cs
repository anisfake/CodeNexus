using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.Lessons.Queries.GetLearningPathGenerationWorkItems;

public record GetLearningPathGenerationWorkItemsQuery(Guid PathId)
    : IRequest<Result<LearningPathGenerationWorkItemsDto>>;

public sealed record LearningPathGenerationWorkItemsDto(
    Guid PathId,
    List<Guid> PendingLessonIds,
    List<LessonPendingQuizDto> PendingQuizzesByLesson);

public sealed record LessonPendingQuizDto(Guid LessonId, List<Guid> QuizIds);

