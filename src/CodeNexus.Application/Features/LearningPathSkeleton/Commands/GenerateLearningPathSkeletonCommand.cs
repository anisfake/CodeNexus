using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPaths.Commands.GenerateLearningPathSkeleton;

public record GenerateLearningPathSkeletonCommand(Guid SubjectId, Guid GoalId) : IRequest<Result<CreateLearningPathResponse>>;
