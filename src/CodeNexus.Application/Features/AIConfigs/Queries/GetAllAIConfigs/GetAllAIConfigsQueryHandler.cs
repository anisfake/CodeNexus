using MediatR;
using CodeNexus.Application.Features.AIConfigs.DTOs;
using CodeNexus.Application.Common.Models;
using Microsoft.Extensions.Caching.Memory;
using CodeNexus.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
                    var configData = ParseConfigJson(config.ConfigJson);
                    var chatPolicy = ExtractObject(configData, "chatPolicy");

                    return new GetAllAIConfigResponse(
                        ConfigId: config.ConfigId,
                        ProviderName: config.ProviderName,
                        UsageType: config.UsageType,
                        AccessTier: config.AccessTier,
                        IsActive: config.IsActive,
                        LastUpdated: config.LastUpdated,
                        ConfigJson: configData,
                        ChatPolicy: chatPolicy
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

        private static Dictionary<string, object> ParseConfigJson(string? configJson)
        {
            if (string.IsNullOrWhiteSpace(configJson))
            {
                return new Dictionary<string, object>();
            }

            try
            {
                using var document = JsonDocument.Parse(configJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return new Dictionary<string, object>();
                }

                return ConvertObject(document.RootElement);
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        private static Dictionary<string, object> ExtractObject(
            Dictionary<string, object> root,
            string propertyName)
        {
            foreach (var kvp in root)
            {
                if (!string.Equals(kvp.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (kvp.Value is Dictionary<string, object> nested)
                {
                    return nested;
                }

                break;
            }

            return new Dictionary<string, object>();
        }

        private static Dictionary<string, object> ConvertObject(JsonElement element)
        {
            var result = new Dictionary<string, object>();
            foreach (var property in element.EnumerateObject())
            {
                var converted = ConvertValue(property.Value);
                if (converted is not null)
                {
                    result[property.Name] = converted;
                }
            }

            return result;
        }

        private static object? ConvertValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => ConvertObject(element),
                JsonValueKind.Array => element.EnumerateArray()
                    .Select(ConvertValue)
                    .Where(v => v is not null)
                    .Cast<object>()
                    .ToList(),
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }
    }
}
