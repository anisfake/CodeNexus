using MediatR;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AIConfigs.DTOs;
using CodeNexus.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CodeNexus.Application.Features.AIConfigs.Commands.UpdateAIConfig;

public class UpdateAIConfigCommandHandler : IRequestHandler<UpdateAIConfigCommand, Result<UpdateAIConfigResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IEncryptionService _encryptionService;

    public UpdateAIConfigCommandHandler(
        IApplicationDbContext context,
        IEncryptionService encryptionService)
    {
        _context = context;
        _encryptionService = encryptionService;
    }

    public async Task<Result<UpdateAIConfigResponse>> Handle(UpdateAIConfigCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var config = await _context.AIProviderConfigs
                .FirstOrDefaultAsync(x => x.ConfigId == request.ConfigId, cancellationToken);

            if (config == null)
                return Result<UpdateAIConfigResponse>.Failure("CONFIG_NOT_FOUND", $"Config with ID '{request.ConfigId}' not found");

            var targetUsageType = request.UsageType ?? config.UsageType;
            var targetAccessTier = request.AccessTier ?? config.AccessTier;
            var targetIsActive = request.IsActive ?? config.IsActive;

            if (targetIsActive)
            {
                var sameGroupActive = await _context.AIProviderConfigs
                    .Where(x => x.ConfigId != config.ConfigId
                                && x.UsageType == targetUsageType
                                && x.AccessTier == targetAccessTier
                                && x.IsActive)
                    .ToListAsync(cancellationToken);

                foreach (var item in sameGroupActive)
                {
                    item.IsActive = false;
                    item.LastUpdated = DateTime.UtcNow;
                }

                if (sameGroupActive.Count > 0)
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            if (request.ApiKey != null)
            {
                config.EncryptedApiKey = _encryptionService.Encrypt(request.ApiKey);
            }

            if (request.ProviderName != null)
            {
                config.ProviderName = request.ProviderName;
            }

            if (request.ConfigJson != null)
            {
                config.ConfigJson = JsonSerializer.Serialize(request.ConfigJson);
            }

            config.IsActive = targetIsActive;
            config.UsageType = targetUsageType;
            config.AccessTier = targetAccessTier;

            config.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return Result<UpdateAIConfigResponse>.Success(new UpdateAIConfigResponse(
                "Config updated successfully",
                config.ProviderName,
                config.IsActive
            ));
        }
        catch (Exception ex)
        {
            return Result<UpdateAIConfigResponse>.Failure("ERROR", ex.Message);
        }
    }
}
