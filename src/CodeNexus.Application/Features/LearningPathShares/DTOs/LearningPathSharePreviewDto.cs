using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.LearningPathShares.DTOs;

public record LearningPathSharePreviewDto(
    Guid ShareId,
    Guid MentorId,
    string MentorName,
    Guid StudentId,
    string StudentName,
    LearningPathShareStatus Status,
    DateTime SentAt,
    DateTime? RespondedAt,
    LearningPathResponse LearningPath
);
