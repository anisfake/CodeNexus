using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateGoalSupplementLearningPath;

public record GenerateGoalSupplementLearningPathCommand(
    Guid SourcePathId,
    Guid GoalId,
    ComplexityLevel? ComplexityLevel = null,
    LanguageSelection? LanguageSelection = null,
    bool SaveAsDraft = false) : IRequest<Result<GoalSupplementLearningPathResponse>>;
