namespace CodeNexus.Application.Features.LearningPathShares.DTOs;

public record LearningPathShareUpdateContextDto(
    Guid ShareId,
    Guid SourceLearningPathId,
    string SourceLearningPathTitle,
    Guid? AcceptedPathId,
    Guid MentorId,
    string MentorUserName,
    decimal CurrentSourceVersion,
    decimal LatestSourceVersion,
    bool HasNewVersion,
    decimal? IgnoredSourceVersion,
    decimal? LastNotifiedSourceVersion,
    LearningPathShareUpdateChangeSummaryDto? ChangeSummary
);
