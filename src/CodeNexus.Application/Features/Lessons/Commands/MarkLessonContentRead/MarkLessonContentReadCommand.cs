using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.Lessons.Commands.MarkLessonContentRead;

public record MarkLessonContentReadCommand(Guid LessonId) : IRequest<Result<string>>;
