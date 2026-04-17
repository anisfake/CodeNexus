using System.Text.Json;
using System.Globalization;

namespace CodeNexus.Application.Features.AIUsageLogs;

public readonly record struct AIUsageCostRate(decimal InputCostPer1M, decimal OutputCostPer1M);

public static class AIUsageCostCalculator
{
    private const decimal OneMillion = 1_000_000m;

    public static decimal CalculateCostUsd(int inputTokens, int outputTokens, AIUsageCostRate rate)
    {
        if (rate.InputCostPer1M <= 0m && rate.OutputCostPer1M <= 0m)
        {
            return 0m;
        }

        var inputCost = ((decimal)Math.Max(inputTokens, 0) / OneMillion) * Math.Max(rate.InputCostPer1M, 0m);
        var outputCost = ((decimal)Math.Max(outputTokens, 0) / OneMillion) * Math.Max(rate.OutputCostPer1M, 0m);
        return decimal.Round(inputCost + outputCost, 8, MidpointRounding.AwayFromZero);
    }

    public static Dictionary<Guid, AIUsageCostRate> BuildRateMap(IEnumerable<(Guid ConfigId, string? ConfigJson)> configs)
    {
        var map = new Dictionary<Guid, AIUsageCostRate>();
        foreach (var config in configs)
        {
            map[config.ConfigId] = ParseRate(config.ConfigJson);
        }

        return map;
    }

    public static AIUsageCostRate ParseRate(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return new AIUsageCostRate(0m, 0m);
        }

        try
        {
            using var document = JsonDocument.Parse(configJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new AIUsageCostRate(0m, 0m);
            }

            var root = document.RootElement;
            var input = TryReadDecimal(root, "InputCostPer1M");
            var output = TryReadDecimal(root, "OutputCostPer1M");
            return new AIUsageCostRate(input, output);
        }
        catch
        {
            return new AIUsageCostRate(0m, 0m);
        }
    }

    private static decimal TryReadDecimal(JsonElement root, string propertyName)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = property.Value;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var decimalValue))
            {
                return decimalValue;
            }

            if (value.ValueKind == JsonValueKind.String
                && decimal.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0m;
    }
}
