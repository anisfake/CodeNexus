using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.SystemRuntimePolicies;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Payments;

public sealed record TokenPricingPolicySnapshot(
    decimal VndPerToken,
    decimal MinCustomTopUpVnd,
    decimal MaxCustomTopUpVnd);

public static class TokenPricingPolicyResolver
{
    public static async Task<TokenPricingPolicySnapshot> ResolveAsync(
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var defaults = new TokenPricingPolicySnapshot(
            TokenPricingConstants.DefaultCustomTopUpVndPerToken,
            TokenPricingConstants.DefaultMinCustomTopUpVnd,
            TokenPricingConstants.DefaultMaxCustomTopUpVnd);

        try
        {
            var policy = await context.SystemRuntimePolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.PolicyKey == TokenPricingConstants.TokenPricingPolicyKey && x.IsActive,
                    cancellationToken);

            if (policy == null)
            {
                return defaults;
            }

            var config = SystemRuntimePolicyJsonHelper.ParseConfigJson(policy.ConfigJson);

            var vndPerToken = ReadPositiveDecimal(config, TokenPricingConstants.VndPerTokenConfigKey, defaults.VndPerToken);
            var minCustomTopUp = ReadPositiveDecimal(config, TokenPricingConstants.MinCustomTopUpVndConfigKey, defaults.MinCustomTopUpVnd);
            var maxCustomTopUp = ReadPositiveDecimal(config, TokenPricingConstants.MaxCustomTopUpVndConfigKey, defaults.MaxCustomTopUpVnd);

            if (maxCustomTopUp < minCustomTopUp)
            {
                maxCustomTopUp = minCustomTopUp;
            }

            return new TokenPricingPolicySnapshot(
                vndPerToken,
                minCustomTopUp,
                maxCustomTopUp);
        }
        catch
        {
            return defaults;
        }
    }

    private static decimal ReadPositiveDecimal(
        IReadOnlyDictionary<string, object> config,
        string key,
        decimal fallback)
    {
        if (!config.TryGetValue(key, out var rawValue))
        {
            return fallback;
        }

        var parsed = rawValue switch
        {
            decimal d when d > 0m => d,
            double d when d > 0d => (decimal)d,
            float f when f > 0f => (decimal)f,
            int i when i > 0 => i,
            long l when l > 0 => l,
            string s when decimal.TryParse(s, out var value) && value > 0m => value,
            _ => 0m
        };

        return parsed > 0m ? parsed : fallback;
    }
}
