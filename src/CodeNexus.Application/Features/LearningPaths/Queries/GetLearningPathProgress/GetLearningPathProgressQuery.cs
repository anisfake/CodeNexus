using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPaths.Queries.GetLearningPathProgress;

public record GetLearningPathProgressQuery(Guid PathId) : IRequest<Result<LearningPathCompletionProgressDto>>;
