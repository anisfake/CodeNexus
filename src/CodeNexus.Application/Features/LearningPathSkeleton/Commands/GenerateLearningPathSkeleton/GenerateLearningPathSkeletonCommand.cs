using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;

public record GenerateLearningPathSkeletonCommand(
    Guid SubjectId, 
    Guid GoalId, 
    ComplexityLevel ComplexityLevel) : IRequest<Result<CreateLearningPathResponse>>;