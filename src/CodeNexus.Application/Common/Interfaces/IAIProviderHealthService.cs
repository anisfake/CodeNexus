using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIConfigs.DTOs;

namespace CodeNexus.Application.Common.Interfaces;

public interface IAIProviderHealthService
{
    Task<Result<TestStoredAIProviderResponse>> TestStoredApiKeyByConfigIdAsync(
        Guid configId,
        CancellationToken cancellationToken = default);
}
