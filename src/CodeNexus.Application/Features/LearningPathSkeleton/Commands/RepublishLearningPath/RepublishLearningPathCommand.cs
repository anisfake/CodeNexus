using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.RepublishLearningPath;

public record RepublishLearningPathCommand(Guid PathId) : IRequest<Result>;
