using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.Lessons.Commands.ConfirmLessonContent;

public record ConfirmLessonContentCommand(Guid LessonId, string Content) : IRequest<Result>;
