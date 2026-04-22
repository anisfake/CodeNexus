using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetMyPublishedLearningPaths;

public record GetMyPublishedLearningPathsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    Guid? SubjectId = null,
    bool SortDescending = true
) : IRequest<Result<PaginationDto<LearningPathResponse>>>;
