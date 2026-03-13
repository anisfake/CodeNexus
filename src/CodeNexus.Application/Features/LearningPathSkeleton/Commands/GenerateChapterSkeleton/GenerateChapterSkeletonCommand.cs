using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathSkeleton.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateChapterSkeleton;

public record GenerateChapterSkeletonCommand(
    Guid PathId,
    int OrderIndex
) : IRequest<Result<ChapterSkeletonDto>>;
