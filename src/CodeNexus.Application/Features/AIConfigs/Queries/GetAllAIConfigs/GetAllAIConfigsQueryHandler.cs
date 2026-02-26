using MediatR;
using CodeNexus.Application.Features.AIConfigs.DTOs;
using CodeNexus.Application.Common.Models;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using CodeNexus.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.AIConfigs.Queries.GetAllAIConfigs
{
    public class GetAllAIConfigsQueryHandler : IRequestHandler<GetAllAIConfigsQuery, Result<List<GetAllAIConfigResponse>>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private const string CACHE_KEY_ALL = "ai_configs_all";
        private const int CACHE_DURATION_MINUTES = 5;

        public GetAllAIConfigsQueryHandler(
            IApplicationDbContext context,
            IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<Result<List<GetAllAIConfigResponse>>> Handle(GetAllAIConfigsQuery request, CancellationToken cancellationToken)
        {
            _cache.Remove(CACHE_KEY_ALL);
            try
            {
                if (_cache.TryGetValue(CACHE_KEY_ALL, out List<GetAllAIConfigResponse>? cachedConfigs))
                {
                    return Result<List<GetAllAIConfigResponse>>.Success(cachedConfigs!);
                }

                var configs = await _context.AIProviderConfigs.ToListAsync(cancellationToken);

                var responses = configs.Select(config =>
                {
                    Dictionary<string, object> configData;
                    try
                    {
                        configData = JsonConvert.DeserializeObject<Dictionary<string, object>>(config.ConfigJson)
                            ?? new Dictionary<string, object>();
                    }
                    catch
                    {
                        configData = new Dictionary<string, object>();
                    }

                    return new GetAllAIConfigResponse(
                        ApiKey: config.EncryptedApiKey,
                        ProviderName: config.ProviderName,
                        IsEnabled: config.IsEnabled,
                        LastUpdated: config.LastUpdated,
                        ConfigJson: configData
                    );
                }).ToList();

                _cache.Set(CACHE_KEY_ALL, responses, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

                return Result<List<GetAllAIConfigResponse>>.Success(responses);
            }
            catch (Exception ex)
            {
                return Result<List<GetAllAIConfigResponse>>.Failure("GET_ALL_CONFIGS_ERROR", ex.Message);
            }
        }
    }
}
