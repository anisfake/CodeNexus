using MediatR;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AIConfigs.DTOs;
using CodeNexus.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using CodeNexus.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using MassTransit;

namespace CodeNexus.Application.Features.AIConfigs.Commands.CreateAIConfig
{
    public class CreateAIConfigCommandHandler : IRequestHandler<CreateAIConfigCommand, Result<CreateAIConfigResponse>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly IMemoryCache _cache;
        private const string CACHE_KEY_ALL = "ai_configs_all";

        public CreateAIConfigCommandHandler(
            IApplicationDbContext context,
            IEncryptionService encryptionService,
            IMemoryCache cache)
        {
            _context = context;
            _encryptionService = encryptionService;
            _cache = cache;
        }

        public async Task<Result<CreateAIConfigResponse>> Handle(CreateAIConfigCommand request, CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var encryptedApiKey in _context.AIProviderConfigs
                    .Select(x => x.EncryptedApiKey)
                    .AsAsyncEnumerable()
                    .WithCancellation(cancellationToken))
                {
                    var decryptedKey = _encryptionService.Decrypt(encryptedApiKey);
                    if (decryptedKey.Equals(request.ApiKey, StringComparison.Ordinal))
                    {
                        return Result<CreateAIConfigResponse>.Failure("DUPLICATE_KEY", "An AI config with the same API key already exists.");
                    }
                }

                var encryptedKey = _encryptionService.Encrypt(request.ApiKey);

                var configJsonString = JsonSerializer.Serialize(request.ConfigJson);

                var config = new AIProviderConfig
                {
                    ConfigId = NewId.NextGuid(),
                    ProviderName = request.ProviderName,
                    EncryptedApiKey = encryptedKey,
                    ConfigJson = configJsonString,
                    IsActive = request.IsEnabled,
                    LastUpdated = DateTime.UtcNow,
                    UsageType = request.AIUsageType,
                    AccessTier = request.AccessTier
                };

                if (config.IsActive)
                {
                    var sameGroupActive = await _context.AIProviderConfigs
                        .Where(x => x.UsageType == config.UsageType
                                    && x.AccessTier == config.AccessTier
                                    && x.IsActive)
                        .ToListAsync(cancellationToken);

                    foreach (var item in sameGroupActive)
                    {
                        item.IsActive = false;
                        item.LastUpdated = DateTime.UtcNow;
                    }
                }

                _context.AIProviderConfigs.Add(config);
                await _context.SaveChangesAsync(cancellationToken);

                _cache.Remove(CACHE_KEY_ALL);

                return Result<CreateAIConfigResponse>.Success(new CreateAIConfigResponse(
                    "Config added successfully",
                    request.ProviderName,
                    request.IsEnabled
                ));
            }
            catch (Exception ex)
            {
                return Result<CreateAIConfigResponse>.Failure("ERROR", ex.Message);
            }
        }
    }
}
