using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Tasks.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Tasks.Queries.GetChapterTasks;

public record GetChapterTasksQuery(Guid ChapterId) : IRequest<Result<ChapterTasksDto>>;
