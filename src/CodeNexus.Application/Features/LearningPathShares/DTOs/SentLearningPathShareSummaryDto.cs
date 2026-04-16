using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.LearningPathShares.DTOs;

public record SentLearningPathShareSummaryDto(
    Guid ShareId,
    Guid PathId,
    Guid? AcceptedPathId,
    string LearningPathTitle,
    string? LearningPathDescription,
    Guid StudentId,
    string StudentName,
    Guid MentorId,
    string MentorName,
    LearningPathShareStatus? Status,
    DateTime SentAt,
    DateTime? RespondedAt
);
