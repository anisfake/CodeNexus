using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Lessons.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Lessons.Commands.GenerateLessonContent;

public record GenerateLessonContentCommand(Guid LessonId) : IRequest<Result<LessonContentDto>>;
