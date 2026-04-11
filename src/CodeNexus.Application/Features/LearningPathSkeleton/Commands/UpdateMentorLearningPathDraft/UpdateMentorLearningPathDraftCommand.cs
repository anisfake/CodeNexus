using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.UpdateMentorLearningPathDraft;

public record UpdateMentorLearningPathDraftCommand(
    Guid PathId,
    bool IncreaseVersion,
    DraftVersionUpdateType? VersionUpdateType,
    Guid SubjectId,
    List<LearningPathGoalRequest> Goals,
    ComplexityLevel ComplexityLevel,
    LanguageSelection LanguageSelection,
    string Title,
    string? Description,
    DateTime StartDate,
    DateTime EndDate,
    List<ManualChapterRequest> Chapters
) : IRequest<Result<CreateLearningPathResponse>>;
