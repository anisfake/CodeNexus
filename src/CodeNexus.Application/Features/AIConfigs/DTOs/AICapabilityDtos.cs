using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.AIConfigs.DTOs;

public record AICapabilityByUsageDto(
    AIUsageType UsageType,
    bool HasFreeConfig,
    bool HasPaidConfig
);

public record GetAICapabilityResponse(
    bool HasPaidAccess,
    string CurrentPlan,
    List<AICapabilityByUsageDto> Capabilities
);
