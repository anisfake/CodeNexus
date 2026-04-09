using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.LearningPathSkeleton.DTOs;

public record GeneratedChapterMentorSkeletonDto(
    Guid PathId,
    string LearningPathTitle,
    LanguageSelection Language,
    ComplexityLevel ComplexityLevel,
    int RecommendedChapterCount,
    string ChapterTitle,
    string? ChapterDescription,
    List<GeneratedChapterMentorLessonItemDto> Lessons
);

public record GeneratedChapterMentorLessonItemDto(
    string Title,
    int OrderIndex
);
