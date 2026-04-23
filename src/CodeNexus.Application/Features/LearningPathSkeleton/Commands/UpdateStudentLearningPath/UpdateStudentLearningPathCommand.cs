using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.UpdateStudentLearningPath;

public record UpdateStudentLearningPathCommand(
    Guid PathId,
    List<StudentChapterRequest> Chapters
) : IRequest<Result<CreateLearningPathResponse>>;
