using System.Text.Json;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.SystemRuntimePolicies;
using CodeNexus.Application.Features.SystemRuntimePolicies.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CodeNexus.Infrastructure.Services;

public class SystemRuntimePolicyService : ISystemRuntimePolicyService
{
    private const string RuntimePolicyKey = "runtime_policy";

    private const int DefaultFocusSessionAutoPauseAfterMinutes = 10;
    private const int DefaultFocusSessionAutoAbandonAfterMinutes = 720;
    private const int DefaultFocusSessionMonitorIntervalSeconds = 60;
    private const int DefaultPendingPaymentTimeoutMinutes = 15;
    private const int DefaultPendingPaymentMonitorIntervalSeconds = 60;
    private const int DefaultOverdueNotificationIntervalMinutes = 15;

    private readonly IApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public SystemRuntimePolicyService(IApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<SystemRuntimePolicyDto?> GetPolicyAsync(string policyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(policyKey))
        {
            return null;
        }

        try
        {
            var normalizedPolicyKey = policyKey.Trim();
            var policy = await _context.SystemRuntimePolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PolicyKey == normalizedPolicyKey, cancellationToken);

            if (policy == null)
            {
                return null;
            }

            return new SystemRuntimePolicyDto(
                policy.SystemRuntimePolicyId,
                policy.PolicyKey,
                policy.Description,
                SystemRuntimePolicyJsonHelper.ParseConfigJson(policy.ConfigJson),
                policy.IsActive,
                policy.UpdatedAt);
        }
        catch
        {
            // Fallback for environments not yet migrated.
            return null;
        }
    }

    public async Task<RuntimeOperationalPolicy> GetRuntimeOperationalPolicyAsync(CancellationToken cancellationToken = default)
    {
        var defaults = new RuntimeOperationalPolicy(
            FocusSessionAutoPauseAfterMinutes: GetConfigInt(
                "SystemRuntimePolicy:ConfigJson:focusSessionAutoPauseAfterMinutes",
                "SystemRuntimePolicy:FocusSessionAutoPauseAfterMinutes",
                DefaultFocusSessionAutoPauseAfterMinutes,
                minValue: 1),
            FocusSessionAutoAbandonAfterMinutes: GetConfigInt(
                "SystemRuntimePolicy:ConfigJson:focusSessionAutoAbandonAfterMinutes",
                "SystemRuntimePolicy:FocusSessionAutoAbandonAfterMinutes",
                DefaultFocusSessionAutoAbandonAfterMinutes,
                minValue: 10),
            FocusSessionMonitorIntervalSeconds: GetConfigInt(
                "SystemRuntimePolicy:ConfigJson:focusSessionMonitorIntervalSeconds",
                "SystemRuntimePolicy:FocusSessionMonitorIntervalSeconds",
                DefaultFocusSessionMonitorIntervalSeconds,
                minValue: 15),
            PendingPaymentTimeoutMinutes: GetConfigInt(
                "SystemRuntimePolicy:ConfigJson:pendingPaymentTimeoutMinutes",
                "SystemRuntimePolicy:PendingPaymentTimeoutMinutes",
                DefaultPendingPaymentTimeoutMinutes,
                minValue: 1),
            PendingPaymentMonitorIntervalSeconds: GetConfigInt(
                "SystemRuntimePolicy:ConfigJson:pendingPaymentMonitorIntervalSeconds",
                "SystemRuntimePolicy:PendingPaymentMonitorIntervalSeconds",
                DefaultPendingPaymentMonitorIntervalSeconds,
                minValue: 15),
            OverdueNotificationIntervalMinutes: GetConfigInt(
                "SystemRuntimePolicy:ConfigJson:overdueNotificationIntervalMinutes",
                "SystemRuntimePolicy:OverdueNotificationIntervalMinutes",
                DefaultOverdueNotificationIntervalMinutes,
                minValue: 1));

        var dbPolicy = await GetPolicyAsync(RuntimePolicyKey, cancellationToken);
        if (dbPolicy is null || !dbPolicy.IsActive)
        {
            return defaults;
        }

        var config = dbPolicy.ConfigJson ?? new Dictionary<string, object>();

        return new RuntimeOperationalPolicy(
            FocusSessionAutoPauseAfterMinutes: ReadInt(config, "focusSessionAutoPauseAfterMinutes", defaults.FocusSessionAutoPauseAfterMinutes, 1),
            FocusSessionAutoAbandonAfterMinutes: ReadInt(config, "focusSessionAutoAbandonAfterMinutes", defaults.FocusSessionAutoAbandonAfterMinutes, 10),
            FocusSessionMonitorIntervalSeconds: ReadInt(config, "focusSessionMonitorIntervalSeconds", defaults.FocusSessionMonitorIntervalSeconds, 15),
            PendingPaymentTimeoutMinutes: ReadInt(config, "pendingPaymentTimeoutMinutes", defaults.PendingPaymentTimeoutMinutes, 1),
            PendingPaymentMonitorIntervalSeconds: ReadInt(config, "pendingPaymentMonitorIntervalSeconds", defaults.PendingPaymentMonitorIntervalSeconds, 15),
            OverdueNotificationIntervalMinutes: ReadInt(config, "overdueNotificationIntervalMinutes", defaults.OverdueNotificationIntervalMinutes, 1));
    }

    private int GetConfigInt(string configJsonPath, string legacyPath, int fallback, int minValue)
    {
        var raw = _configuration[configJsonPath] ?? _configuration[legacyPath];
        if (int.TryParse(raw, out var parsed) && parsed >= minValue)
        {
            return parsed;
        }

        return fallback;
    }

    private static int ReadInt(
        Dictionary<string, object> config,
        string key,
        int fallback,
        int minValue)
    {
        if (!TryGetValueIgnoreCase(config, key, out var rawValue))
        {
            return fallback;
        }

        if (!TryConvertToInt(rawValue, out var parsed) || parsed < minValue)
        {
            return fallback;
        }

        return parsed;
    }

    private static bool TryGetValueIgnoreCase(
        Dictionary<string, object> config,
        string key,
        out object value)
    {
        foreach (var item in config)
        {
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = item.Value;
                return true;
            }
        }

        value = default!;
        return false;
    }

    private static bool TryConvertToInt(object value, out int parsed)
    {
        switch (value)
        {
            case int intValue:
                parsed = intValue;
                return true;
            case long longValue when longValue is <= int.MaxValue and >= int.MinValue:
                parsed = (int)longValue;
                return true;
            case double doubleValue when doubleValue is <= int.MaxValue and >= int.MinValue:
                parsed = (int)doubleValue;
                return true;
            case decimal decimalValue when decimalValue is <= int.MaxValue and >= int.MinValue:
                parsed = (int)decimalValue;
                return true;
            case JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number && jsonElement.TryGetInt32(out parsed):
                return true;
            case JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String && int.TryParse(jsonElement.GetString(), out parsed):
                return true;
            case string text when int.TryParse(text, out parsed):
                return true;
            default:
                parsed = 0;
                return false;
        }
    }
}
