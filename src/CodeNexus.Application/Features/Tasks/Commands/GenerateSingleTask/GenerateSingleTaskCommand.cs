using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Tasks.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.Tasks.Commands.GenerateSingleTask;

public record GenerateSingleTaskCommand(Guid ChapterId, string? Title, TaskType TaskType) : IRequest<Result<TaskItemDto>>;
