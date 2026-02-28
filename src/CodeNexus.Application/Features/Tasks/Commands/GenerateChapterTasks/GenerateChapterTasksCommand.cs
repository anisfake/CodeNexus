using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Tasks.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Tasks.Commands.GenerateChapterTasks;

public record GenerateChapterTasksCommand(Guid ChapterId) : IRequest<Result<ChapterTasksDto>>;
