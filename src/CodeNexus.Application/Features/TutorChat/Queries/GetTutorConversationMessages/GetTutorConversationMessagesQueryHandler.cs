using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TutorChat.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CodeNexus.Application.Features.TutorChat.Queries.GetTutorConversationMessages;

public class GetTutorConversationMessagesQueryHandler
    : IRequestHandler<GetTutorConversationMessagesQuery, Result<TutorMessagesPageDto>>
{
    private const string DefaultAssistantModel = "meta-llama/llama-4-scout-17b-16e-instruct";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetTutorConversationMessagesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<TutorMessagesPageDto>> Handle(GetTutorConversationMessagesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConversationId == request.ConversationId && !c.IsDeleted, cancellationToken);

        if (conversation == null)
        {
            return Result<TutorMessagesPageDto>.Failure("CONVERSATION_NOT_FOUND", "Conversation not found.");
        }

        if (conversation.UserId != userId)
        {
            return Result<TutorMessagesPageDto>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var totalCount = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == request.ConversationId)
            .CountAsync(cancellationToken);

        var messages = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == request.ConversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var items = messages.Select(m =>
        {
            var role = ParseRole(m.Content);
            var content = StripRolePrefix(m.Content);
            return new TutorMessageDto(
                m.MessageId,
                m.ConversationId,
                role,
                content,
                m.CreatedAt
            );
        }).ToList();

        var contextUsagePercent = await ResolveContextUsagePercentAsync(
            request.ConversationId,
            userId,
            conversation.ConfigId,
            cancellationToken);

        return Result<TutorMessagesPageDto>.Success(new TutorMessagesPageDto
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            ContextUsagePercent = contextUsagePercent
        });
    }

    private async Task<double> ResolveContextUsagePercentAsync(
        Guid conversationId,
        Guid userId,
        Guid configId,
        CancellationToken cancellationToken)
    {
        var latestAssistantMessageAt = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.Content.StartsWith("ASSISTANT:", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => (DateTime?)m.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (!latestAssistantMessageAt.HasValue)
        {
            return 0d;
        }

        var usageLog = await _context.AIUsageLogs
            .AsNoTracking()
            .Where(log =>
                log.UserId == userId
                && log.UsageType == AIUsageType.Assistant
                && log.CreatedAt <= latestAssistantMessageAt.Value.AddSeconds(30))
            .OrderByDescending(log => log.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (usageLog == null)
        {
            return 0d;
        }

        var config = await _context.AIProviderConfigs
            .AsNoTracking()
            .Where(c => c.ConfigId == configId)
            .Select(c => new { c.ConfigJson })
            .FirstOrDefaultAsync(cancellationToken);

        var (modelName, configuredContextWindow) = ParseModelHints(config?.ConfigJson);
        var normalizedModelName = string.IsNullOrWhiteSpace(modelName)
            ? (string.IsNullOrWhiteSpace(usageLog.Model) ? DefaultAssistantModel : usageLog.Model)
            : modelName.Trim();
        var contextWindow = configuredContextWindow ?? InferContextWindowFromModel(normalizedModelName);

        if (contextWindow <= 0)
        {
            return 0d;
        }

        var usagePercent = (usageLog.InputTokens / (double)contextWindow) * 100d;
        return Math.Round(Math.Clamp(usagePercent, 0d, 100d), 2);
    }

    private static string ParseRole(string content)
    {
        if (content.StartsWith("USER:", StringComparison.OrdinalIgnoreCase))
            return "user";
        if (content.StartsWith("ASSISTANT:", StringComparison.OrdinalIgnoreCase))
            return "assistant";
        return "unknown";
    }

    private static string StripRolePrefix(string content)
    {
        if (content.StartsWith("USER:", StringComparison.OrdinalIgnoreCase))
            return content.Substring("USER:".Length).Trim();
        if (content.StartsWith("ASSISTANT:", StringComparison.OrdinalIgnoreCase))
            return content.Substring("ASSISTANT:".Length).Trim();
        return content.Trim();
    }

    private static (string? ModelName, int? ContextWindow) ParseModelHints(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(configJson);
            var root = document.RootElement;

            string? modelName = null;
            int? contextWindow = null;

            if (TryGetStringPropertyIgnoreCase(root, "model", out var parsedModel))
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
        catch
        {
            return (null, null);
        }
    }

    private static bool TryGetStringPropertyIgnoreCase(JsonElement root, string propertyName, out string? value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
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

    private static int InferContextWindowFromModel(string modelName)
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
}
