using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetAllLearningPaths;

public record GetAllLearningPathQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    Guid? SubjectId = null,
    LearningPathStatus? Status = null,
    bool SortDescending = true
) : IRequest<Result<PaginationDto<LearningPathResponse>>>;
