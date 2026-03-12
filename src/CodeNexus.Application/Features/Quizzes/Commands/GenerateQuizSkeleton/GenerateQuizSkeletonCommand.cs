using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Quizzes.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Quizzes.Commands.GenerateQuizSkeleton;

public record GenerateQuizSkeletonCommand(Guid LessonId) : IRequest<Result<GeneratedQuizSkeletonDto>>;