namespace CodeNexus.Application.Features.LearningPathShares.DTOs;

public record LearningPathShareUpdateContextDto(
    Guid ShareId,
    Guid SourceLearningPathId,
    string SourceLearningPathTitle,
    Guid? AcceptedPathId,
    Guid MentorId,
    string MentorUserName,
    int CurrentSourceVersion,
    int LatestSourceVersion,
    bool HasNewVersion,
    int? IgnoredSourceVersion,
    int? LastNotifiedSourceVersion
);
