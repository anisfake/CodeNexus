using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Quizzes.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Quizzes.Commands.GenerateSingleQuizSkeleton;

public record GenerateSingleQuizSkeletonCommand(Guid LessonId) : IRequest<Result<QuizSkeletonDto>>;
