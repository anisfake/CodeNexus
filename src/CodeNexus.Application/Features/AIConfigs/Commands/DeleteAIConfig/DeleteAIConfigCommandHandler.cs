using MediatR;
using CodeNexus.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using CodeNexus.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.AIConfigs.Commands.DeleteAIConfig;

public class DeleteAIConfigCommandHandler : IRequestHandler<DeleteAIConfigCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private const string CACHE_KEY_ALL = "ai_configs_all";

    public DeleteAIConfigCommandHandler(
        IApplicationDbContext context,
        IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<Result<string>> Handle(DeleteAIConfigCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var config = await _context.AIProviderConfigs
                .FirstOrDefaultAsync(x => x.ProviderName == request.ProviderName, cancellationToken);

            if (config == null)
                return Result<string>.Failure("PROVIDER_NOT_FOUND", $"Provider '{request.ProviderName}' not found");

            _context.AIProviderConfigs.Remove(config);
            await _context.SaveChangesAsync(cancellationToken);

            _cache.Remove(CACHE_KEY_ALL);

            return Result<string>.Success($"Provider '{request.ProviderName}' deleted successfully");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure("ERROR", ex.Message);
        }
    }
}
