using MediatR;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.AIConfigs.Commands.SetActiveConfig;

public record SetActiveConfigCommand(
    Guid ConfigId,
    AIUsageType UsageType,
    AIAccessTier AccessTier
) : IRequest<Result<string>>;
