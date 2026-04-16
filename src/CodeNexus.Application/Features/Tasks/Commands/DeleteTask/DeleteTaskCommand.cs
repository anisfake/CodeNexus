using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.Tasks.Commands.DeleteTask;

public record DeleteTaskCommand(Guid TaskId) : IRequest<Result>;
