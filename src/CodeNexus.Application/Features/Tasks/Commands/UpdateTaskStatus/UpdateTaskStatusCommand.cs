using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.Tasks.Commands.UpdateTaskStatus;

public record UpdateTaskStatusCommand(Guid TaskId, TaskStatus_ Status) : IRequest<Result>;
