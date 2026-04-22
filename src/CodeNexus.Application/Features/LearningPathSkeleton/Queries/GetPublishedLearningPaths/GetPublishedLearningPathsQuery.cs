using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetPublishedLearningPaths;

public record GetPublishedLearningPathsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    Guid? SubjectId = null,
    ComplexityLevel? ComplexityLevel = null,
    bool SortDescending = true
) : IRequest<Result<PaginationDto<PublishedLearningPathSummaryDto>>>;
