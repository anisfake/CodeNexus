namespace CodeNexus.API.Models.Requests;

public record UpdateSystemRuntimePolicyRequest(
    string? Description,
    Dictionary<string, object>? ConfigJson,
    bool IsActive = true
);
