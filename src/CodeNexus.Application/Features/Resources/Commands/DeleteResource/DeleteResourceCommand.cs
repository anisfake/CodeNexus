using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.Resources.Commands.DeleteResource;

public record DeleteResourceCommand(Guid ResourceId) : IRequest<Result<string>>;
