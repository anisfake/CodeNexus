using MediatR;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CodeNexus.Application.Features.AIConfigs.Commands.SetActiveConfig;

public class SetActiveConfigCommandHandler : IRequestHandler<SetActiveConfigCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private const string CACHE_KEY_ALL = "ai_configs_all";

    public SetActiveConfigCommandHandler(
        IApplicationDbContext context,
        IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<Result<string>> Handle(SetActiveConfigCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var config = await _context.AIProviderConfigs
                .FirstOrDefaultAsync(x => x.ConfigId == request.ConfigId, cancellationToken);

            if (config == null)
                return Result<string>.Failure("CONFIG_NOT_FOUND", $"Config with ID '{request.ConfigId}' not found");

            if (config.UsageType != request.UsageType)
                return Result<string>.Failure("USAGE_TYPE_MISMATCH", $"Config usage type does not match the requested usage type");

            if (config.AccessTier != request.AccessTier)
                return Result<string>.Failure("ACCESS_TIER_MISMATCH", "Config access tier does not match the requested access tier");

            var configsToDeactivate = await _context.AIProviderConfigs
                .Where(x => x.UsageType == request.UsageType
                    && x.AccessTier == request.AccessTier
                    && x.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var c in configsToDeactivate)
            {
                c.IsActive = false;
            }

            config.IsActive = true;
            config.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _cache.Remove(CACHE_KEY_ALL);

            return Result<string>.Success($"Config '{config.ProviderName}' is now active for {request.UsageType} ({request.AccessTier})");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure("ERROR", ex.Message);
        }
    }
}
