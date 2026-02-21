using MediatR;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AIConfigs.DTOs;
using Microsoft.Extensions.Caching.Memory;
using CodeNexus.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CodeNexus.Application.Features.AIConfigs.Commands.UpdateAIConfig;

public class UpdateAIConfigCommandHandler : IRequestHandler<UpdateAIConfigCommand, Result<UpdateAIConfigResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly IMemoryCache _cache;
    private const string CACHE_KEY_ALL = "ai_configs_all";

    public UpdateAIConfigCommandHandler(
        IApplicationDbContext context,
        IEncryptionService encryptionService,
        IMemoryCache cache)
    {
        _context = context;
        _encryptionService = encryptionService;
        _cache = cache;
    }

    public async Task<Result<UpdateAIConfigResponse>> Handle(UpdateAIConfigCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var config = await _context.AIProviderConfigs
                .FirstOrDefaultAsync(x => x.ProviderName == request.ProviderName, cancellationToken);

            if (config == null)
                return Result<UpdateAIConfigResponse>.Failure("PROVIDER_NOT_FOUND", $"Provider '{request.ProviderName}' not found");

            if (request.ApiKey != null)
            {
                config.EncryptedApiKey = _encryptionService.Encrypt(request.ApiKey);
            }

            if (request.ConfigJson != null)
            {
                config.ConfigJson = JsonSerializer.Serialize(request.ConfigJson);
            }

            if (request.IsEnabled.HasValue)
            {
                config.IsEnabled = request.IsEnabled.Value;
            }

            config.LastUpdated = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);

            _cache.Remove(CACHE_KEY_ALL);

            return Result<UpdateAIConfigResponse>.Success(new UpdateAIConfigResponse(
                "Config updated successfully",
                request.ProviderName,
                config.IsEnabled
            ));
        }
        catch (Exception ex)
        {
            return Result<UpdateAIConfigResponse>.Failure("ERROR", ex.Message);
        }
    }
}
