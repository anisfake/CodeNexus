using MediatR;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.AIConfigs.Commands.SetActiveConfig;

public record SetActiveConfigCommand(
    Guid ConfigId,
    AIUsageType UsageType
) : IRequest<Result<string>>;
