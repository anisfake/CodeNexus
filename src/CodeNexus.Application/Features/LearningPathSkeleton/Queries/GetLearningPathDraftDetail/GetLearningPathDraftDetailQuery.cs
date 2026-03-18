using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathDraftDetail;

public record GetLearningPathDraftDetailQuery(Guid PathId) : IRequest<Result<LearningPathResponse>>;
