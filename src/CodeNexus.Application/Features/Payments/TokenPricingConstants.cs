namespace CodeNexus.Application.Features.Payments;

public static class TokenPricingConstants
{
    public const string TokenPricingPolicyKey = "token_pricing_policy";
    public const string VndPerTokenConfigKey = "vndPerToken";
    public const string MinCustomTopUpVndConfigKey = "minCustomTopUpVnd";
    public const string MaxCustomTopUpVndConfigKey = "maxCustomTopUpVnd";

    public const decimal DefaultCustomTopUpVndPerToken = 100m;
    public const decimal DefaultMinCustomTopUpVnd = 10000m;
    public const decimal DefaultMaxCustomTopUpVnd = 1000000m;
}
