namespace CodeNexus.API.Models.Requests;

public record CreateSystemRuntimePolicyRequest(
    string PolicyKey,
    string? Description,
    Dictionary<string, object>? ConfigJson,
    bool IsActive = true
);

