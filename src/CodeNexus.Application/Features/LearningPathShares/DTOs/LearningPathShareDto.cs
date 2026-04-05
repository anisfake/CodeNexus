using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.LearningPathShares.DTOs;

public record LearningPathShareDto(
    Guid ShareId,
    Guid PathId,
    Guid MentorId,
    Guid StudentId,
    LearningPathShareStatus Status,
    DateTime SentAt,
    DateTime? RespondedAt,
    Guid? AcceptedPathId = null,
    int? SourceVersionAtAccept = null,
    int? IgnoredSourceVersion = null,
    int? LastNotifiedSourceVersion = null,
    bool IsTrackingEnabled = true,
    string? InvalidatedReason = null
);
