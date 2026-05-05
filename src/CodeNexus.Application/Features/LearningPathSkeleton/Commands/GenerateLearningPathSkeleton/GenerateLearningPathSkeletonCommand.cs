using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;

public record GenerateLearningPathSkeletonCommand(
    Guid SubjectId,
    List<LearningPathGoalRequest> Goals,
    ComplexityLevel ComplexityLevel,
    LanguageSelection LanguageSelection,
    bool SaveAsDraft = false,
    bool UseAbsoluteGoalWeights = false,
    string? GenerationContextInstruction = null) : IRequest<Result<CreateLearningPathResponse>>;
