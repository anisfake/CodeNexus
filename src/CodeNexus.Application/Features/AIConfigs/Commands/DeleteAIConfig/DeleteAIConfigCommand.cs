using MediatR;
using CodeNexus.Application.Common.Models;

namespace CodeNexus.Application.Features.AIConfigs.Commands.DeleteAIConfig;

public record DeleteAIConfigCommand(Guid ConfigId) : IRequest<Result<string>>;
