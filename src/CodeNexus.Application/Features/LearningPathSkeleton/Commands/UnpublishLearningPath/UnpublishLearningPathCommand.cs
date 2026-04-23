using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.UnpublishLearningPath;

public record UnpublishLearningPathCommand(Guid PathId) : IRequest<Result>;
