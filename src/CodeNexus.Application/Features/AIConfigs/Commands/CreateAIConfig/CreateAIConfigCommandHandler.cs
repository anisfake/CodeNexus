using MediatR;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AIConfigs.DTOs;
using CodeNexus.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using CodeNexus.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
                var existing = await _context.AIProviderConfigs
                    .FirstOrDefaultAsync(x => x.ProviderName == request.ProviderName, cancellationToken);

                if (existing != null)
                    return Result<CreateAIConfigResponse>.Failure("PROVIDER_EXISTS", $"Provider '{request.ProviderName}' already exists");

                var encryptedKey = _encryptionService.Encrypt(request.ApiKey);

                var configJsonString = JsonSerializer.Serialize(request.ConfigJson);

                var config = new AIProviderConfig
                {
                    ProviderName = request.ProviderName,
                    EncryptedApiKey = encryptedKey,
                    ConfigJson = configJsonString,
                    IsEnabled = request.IsEnabled,
                    LastUpdated = DateTime.Now
                };

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