using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Resources.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Resources.Commands.UpdateResource;

public record UpdateResourceCommand(
    Guid ResourceId,
    string? Title,
    string? Description,
    Stream? FilePath,
    string? FileName
) : IRequest<Result<string>>;
