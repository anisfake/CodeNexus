using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathSkeleton.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateChapterMentorSkeleton;

public record GenerateChapterMentorSkeletonCommand(
    Guid PathId,
    string ChapterTitle,
    string? ChapterDescription = null
) : IRequest<Result<GeneratedChapterMentorSkeletonDto>>;
