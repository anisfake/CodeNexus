using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPaths.Commands.GenerateLearningPathSkeleton;

public record GenerateLearningPathSkeletonCommand : IRequest<Result<CreateLearningPathResponse>>
{
    public Guid SubjectId { get; init; }
    public Guid GoalId { get; init; }
}
