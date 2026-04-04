using MediatR;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CodeNexus.Application.Features.AIConfigs.Commands.SetActiveConfig;

public class SetActiveConfigCommandHandler : IRequestHandler<SetActiveConfigCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;

    public SetActiveConfigCommandHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<string>> Handle(SetActiveConfigCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var config = await _context.AIProviderConfigs
                .FirstOrDefaultAsync(x => x.ConfigId == request.ConfigId, cancellationToken);

            if (config == null)
                return Result<string>.Failure("CONFIG_NOT_FOUND", $"Config with ID '{request.ConfigId}' not found");

            var targetUsageType = config.UsageType;
            var targetAccessTier = config.AccessTier;

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

            config.IsActive = true;
            config.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return Result<string>.Success($"Config '{config.ProviderName}' is active for {targetUsageType} ({targetAccessTier})");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure("ERROR", ex.Message);
        }
    }
}
