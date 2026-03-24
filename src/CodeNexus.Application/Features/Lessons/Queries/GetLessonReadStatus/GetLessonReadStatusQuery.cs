using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Lessons.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Lessons.Queries.GetLessonReadStatus;

public record GetLessonReadStatusQuery(Guid LessonId) : IRequest<Result<LessonReadStatusDto>>;
