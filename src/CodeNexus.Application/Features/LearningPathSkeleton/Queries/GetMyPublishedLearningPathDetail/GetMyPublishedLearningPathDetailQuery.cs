using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetMyPublishedLearningPathDetail;

public record GetMyPublishedLearningPathDetailQuery(Guid PathId) : IRequest<Result<LearningPathResponse>>;
