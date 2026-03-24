using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.LearningPathShares.DTOs;

public record LearningPathShareSummaryDto(
    Guid ShareId,
    Guid PathId,
    string LearningPathTitle,
    string? LearningPathDescription,
    Guid MentorId,
    string MentorName,
    LearningPathShareStatus Status,
    DateTime SentAt
);
