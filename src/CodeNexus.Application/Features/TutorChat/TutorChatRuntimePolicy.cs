using System.Text.Json;

namespace CodeNexus.Application.Features.TutorChat;

internal sealed record TutorChatPolicy(
    int RuntimeContextBudget,
    double ReservedOutputRatio,
    int ReservedOutputMin,
    int ReservedOutputMax,
    double SummaryTriggerRatio,
    double ForceSummaryRatio,
    int SummaryMinUnsummarizedMessages,
    int SummaryPreserveRecentMessages,
    int ForceSummaryPreserveRecentMessages,
    int RecentHistoryMinMessages,
    int RecentHistoryMaxMessages,
    int OlderDigestInitialLineLimit,
    int OlderDigestStep,
    int ArchivedSummaryPromptTake,
    int ArchivedSummaryPromptCharLimit,
    int ArchiveSummaryTakeHeadLines,
    int ArchiveSummaryTakeTailLines,
    int ArchiveSummaryLineCharLimit,
    int HistoryMessageCharLimit,
    int HistoryMessageMinCharLimit,
    int OlderHistoryCharLimit,
    int OlderHistoryMinCharLimit,
    int LearningPathDescriptionCharLimit,
    int ChapterContentCharLimit,
    int LessonContentCharLimit);

internal static class TutorChatRuntimePolicy
{
    private static readonly TutorChatPolicy DefaultPolicy = new(
        RuntimeContextBudget: 0,
        ReservedOutputRatio: 0.08,
        ReservedOutputMin: 1024,
        ReservedOutputMax: 4096,
        SummaryTriggerRatio: 0.7,
        ForceSummaryRatio: 0.82,
        SummaryMinUnsummarizedMessages: 20,
        SummaryPreserveRecentMessages: 16,
        ForceSummaryPreserveRecentMessages: 10,
        RecentHistoryMinMessages: 10,
        RecentHistoryMaxMessages: 20,
        OlderDigestInitialLineLimit: 12,
        OlderDigestStep: 2,
        ArchivedSummaryPromptTake: 6,
        ArchivedSummaryPromptCharLimit: 260,
        ArchiveSummaryTakeHeadLines: 6,
        ArchiveSummaryTakeTailLines: 6,
        ArchiveSummaryLineCharLimit: 180,
        HistoryMessageCharLimit: 260,
        HistoryMessageMinCharLimit: 90,
        OlderHistoryCharLimit: 180,
        OlderHistoryMinCharLimit: 100,
        LearningPathDescriptionCharLimit: 600,
        ChapterContentCharLimit: 1800,
        LessonContentCharLimit: 2200);

    public static TutorChatPolicy Resolve(string? configJson)
    {
        var policy = DefaultPolicy;

        if (!TryGetRootJson(configJson, out var root))
        {
            return policy;
        }

        if (!TryGetObjectPropertyIgnoreCase(root, "chatPolicy", out var chatPolicy))
        {
            return policy;
        }

        // Chat policy is admin-managed from configJson. We still clamp values for runtime safety.
        var recentMax = Math.Clamp(
            GetInt(chatPolicy, policy.RecentHistoryMaxMessages, "recentHistoryMaxMessages"),
            4,
            200);
        var recentMin = Math.Clamp(
            GetInt(chatPolicy, policy.RecentHistoryMinMessages, "recentHistoryMinMessages"),
            1,
            recentMax);

        var preserveRecent = Math.Clamp(
            GetInt(chatPolicy, policy.SummaryPreserveRecentMessages, "summaryPreserveRecentMessages"),
            recentMin,
            recentMax);

        var forcePreserveRecent = Math.Clamp(
            GetInt(chatPolicy, policy.ForceSummaryPreserveRecentMessages, "forceSummaryPreserveRecentMessages"),
            1,
            preserveRecent);

        var minUnsummarized = Math.Clamp(
            GetInt(chatPolicy, policy.SummaryMinUnsummarizedMessages, "summaryMinUnsummarizedMessages"),
            preserveRecent + 1,
            1000);

        var reservedOutputRatio = GetRatio(chatPolicy, policy.ReservedOutputRatio, 0.03, 0.3, "reservedOutputRatio");
        var summaryTriggerRatio = GetRatio(chatPolicy, policy.SummaryTriggerRatio, 0.35, 0.9, "summaryTriggerRatio");
        var forceSummaryRatio = GetRatio(chatPolicy, policy.ForceSummaryRatio, 0.4, 0.98, "forceSummaryRatio");

        if (forceSummaryRatio <= summaryTriggerRatio)
        {
            forceSummaryRatio = Math.Min(0.99, summaryTriggerRatio + 0.08);
        }

        return new TutorChatPolicy(
            RuntimeContextBudget: Math.Max(0, GetInt(chatPolicy, policy.RuntimeContextBudget, "runtimeContextBudget")),
            ReservedOutputRatio: reservedOutputRatio,
            ReservedOutputMin: policy.ReservedOutputMin,
            ReservedOutputMax: policy.ReservedOutputMax,
            SummaryTriggerRatio: summaryTriggerRatio,
            ForceSummaryRatio: forceSummaryRatio,
            SummaryMinUnsummarizedMessages: minUnsummarized,
            SummaryPreserveRecentMessages: preserveRecent,
            ForceSummaryPreserveRecentMessages: forcePreserveRecent,
            RecentHistoryMinMessages: recentMin,
            RecentHistoryMaxMessages: recentMax,
            OlderDigestInitialLineLimit: policy.OlderDigestInitialLineLimit,
            OlderDigestStep: policy.OlderDigestStep,
            ArchivedSummaryPromptTake: policy.ArchivedSummaryPromptTake,
            ArchivedSummaryPromptCharLimit: policy.ArchivedSummaryPromptCharLimit,
            ArchiveSummaryTakeHeadLines: policy.ArchiveSummaryTakeHeadLines,
            ArchiveSummaryTakeTailLines: policy.ArchiveSummaryTakeTailLines,
            ArchiveSummaryLineCharLimit: policy.ArchiveSummaryLineCharLimit,
            HistoryMessageCharLimit: policy.HistoryMessageCharLimit,
            HistoryMessageMinCharLimit: policy.HistoryMessageMinCharLimit,
            OlderHistoryCharLimit: policy.OlderHistoryCharLimit,
            OlderHistoryMinCharLimit: policy.OlderHistoryMinCharLimit,
            LearningPathDescriptionCharLimit: policy.LearningPathDescriptionCharLimit,
            ChapterContentCharLimit: policy.ChapterContentCharLimit,
            LessonContentCharLimit: policy.LessonContentCharLimit);
    }

    public static (string ModelName, int ContextWindow) ResolveModelContext(
        string? configJson,
        string defaultModel)
    {
        var (modelName, configuredContextWindow) = ParseModelHints(configJson);
        var normalizedModelName = string.IsNullOrWhiteSpace(modelName) ? defaultModel : modelName.Trim();

        var inferred = InferContextWindowFromModel(normalizedModelName);
        var contextWindow = configuredContextWindow.GetValueOrDefault(inferred);
        if (contextWindow <= 0)
        {
            contextWindow = inferred;
        }

        return (normalizedModelName, contextWindow);
    }

    public static int InferContextWindowFromModel(string modelName)
    {
        var normalized = modelName.ToLowerInvariant();

        if (normalized.Contains("1m") || normalized.Contains("1000k"))
            return 1_000_000;

        if (normalized.Contains("256k"))
            return 256_000;

        if (normalized.Contains("200k"))
            return 200_000;

        if (normalized.Contains("128k")
            || normalized.Contains("llama-4")
            || normalized.Contains("llama-3.3")
            || normalized.Contains("llama-3.1")
            || normalized.Contains("qwen2.5")
            || normalized.Contains("gpt-4.1")
            || normalized.Contains("gpt-5"))
            return 128_000;

        if (normalized.Contains("64k"))
            return 64_000;

        if (normalized.Contains("32k")
            || normalized.Contains("mistral")
            || normalized.Contains("mixtral")
            || normalized.Contains("codestral"))
            return 32_000;

        if (normalized.Contains("16k"))
            return 16_000;

        if (normalized.Contains("8k") || normalized.Contains("gemma"))
            return 8_000;

        return 32_000;
    }

    private static (string? ModelName, int? ContextWindow) ParseModelHints(string? configJson)
    {
        if (!TryGetRootJson(configJson, out var root))
        {
            return (null, null);
        }

        string? modelName = null;
        int? contextWindow = null;

        if (TryGetStringPropertyIgnoreCase(root, out var parsedModel, "model"))
        {
            modelName = parsedModel;
        }

        if (TryGetIntPropertyIgnoreCase(root, out var parsedContextWindow,
                "contextWindow",
                "context_window",
                "maxContextTokens",
                "max_context_tokens"))
        {
            contextWindow = parsedContextWindow;
        }

        return (modelName, contextWindow);
    }

    private static bool TryGetRootJson(string? json, out JsonElement root)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            root = default;
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            root = document.RootElement.Clone();
            return true;
        }
        catch
        {
            root = default;
            return false;
        }
    }

    private static bool TryGetObjectPropertyIgnoreCase(JsonElement root, string propertyName, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                value = property.Value;
                return true;
            }

            break;
        }

        value = default;
        return false;
    }

    private static bool TryGetStringPropertyIgnoreCase(JsonElement root, out string? value, params string[] propertyNames)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!propertyNames.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            value = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.ToString();
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetIntPropertyIgnoreCase(JsonElement root, out int value, params string[] propertyNames)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!propertyNames.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number
                && property.Value.TryGetInt32(out value))
            {
                return true;
            }

            if (property.Value.ValueKind == JsonValueKind.String
                && int.TryParse(property.Value.GetString(), out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryGetDoublePropertyIgnoreCase(JsonElement root, out double value, params string[] propertyNames)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!propertyNames.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number
                && property.Value.TryGetDouble(out value))
            {
                return true;
            }

            if (property.Value.ValueKind == JsonValueKind.String
                && double.TryParse(property.Value.GetString(), out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static int GetInt(JsonElement root, int defaultValue, params string[] propertyNames)
    {
        return TryGetIntPropertyIgnoreCase(root, out var value, propertyNames) ? value : defaultValue;
    }

    private static double GetRatio(JsonElement root, double defaultValue, double min, double max, params string[] propertyNames)
    {
        if (!TryGetDoublePropertyIgnoreCase(root, out var value, propertyNames))
        {
            return defaultValue;
        }

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return defaultValue;
        }

        return Math.Clamp(value, min, max);
    }
}
