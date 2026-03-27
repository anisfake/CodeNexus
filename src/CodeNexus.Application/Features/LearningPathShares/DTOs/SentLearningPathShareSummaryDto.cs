using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.LearningPathShares.DTOs;

public record SentLearningPathShareSummaryDto(
    Guid ShareId,
    Guid PathId,
    string LearningPathTitle,
    string? LearningPathDescription,
    Guid StudentId,
    string StudentName,
    LearningPathShareStatus? Status,
    DateTime SentAt,
    DateTime? RespondedAt
);
