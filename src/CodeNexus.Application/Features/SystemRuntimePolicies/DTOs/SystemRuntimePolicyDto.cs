namespace CodeNexus.Application.Features.SystemRuntimePolicies.DTOs;

public record SystemRuntimePolicyDto(
    Guid? SystemRuntimePolicyId,
    string PolicyKey,
    string? Description,
    Dictionary<string, object> ConfigJson,
    bool IsActive,
    DateTime UpdatedAt
);
