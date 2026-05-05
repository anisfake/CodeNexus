using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.RepublishLearningPath;

public record RepublishLearningPathCommand(
    Guid PathId,
    bool IncreaseVersion,
    DraftVersionUpdateType? VersionUpdateType
) : IRequest<Result>;
