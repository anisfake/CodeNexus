using System.Text.Json;

namespace CodeNexus.Application.Features.SystemRuntimePolicies;

public static class SystemRuntimePolicyJsonHelper
{
    public static Dictionary<string, object> ParseConfigJson(string? configJson)
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

    public static string SerializeConfigJson(Dictionary<string, object>? configJson)
    {
        return JsonSerializer.Serialize(configJson ?? new Dictionary<string, object>());
    }

    private static Dictionary<string, object> ConvertObject(JsonElement element)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
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
