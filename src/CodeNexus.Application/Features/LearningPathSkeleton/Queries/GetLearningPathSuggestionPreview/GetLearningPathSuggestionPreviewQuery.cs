using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathSuggestionPreview;

public record GetLearningPathSuggestionPreviewQuery(
    Guid SuggestedPathId,
    Guid SubjectId,
    List<LearningPathGoalRequest> Goals,
    ComplexityLevel ComplexityLevel,
    LanguageSelection LanguageSelection) : IRequest<Result<LearningPathSuggestionPreviewDto>>;
