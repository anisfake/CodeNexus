using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathDetailByUserId;

public record GetLearningPathDetailByUserIdQuery(
    Guid UserId,
    Guid PathId
) : IRequest<Result<LearningPathResponse>>;
