using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Common.Helpers;

public static class LearningPathGoalSemanticMappingHelper
{
    private const int MaxItemsPerBatch = 24;
    private const decimal MinRelevanceThreshold = 0.05m;

    public static async Task<bool> RebuildForPathAsync(
        IApplicationDbContext context,
        IAIGeneratorService aiGeneratorService,
        Guid pathId,
        LanguageSelection language,
        CancellationToken cancellationToken = default)
    {
        if (context.LearningPathGoals is null
            || context.LearningPathGoalItemMappings is null
            || context.Lessons is null
            || context.Tasks is null
            || context.Quizzes is null)
        {
            return false;
        }

        var goals = await context.LearningPathGoals
            .Where(x => x.PathId == pathId)
            .Select(x => new GoalInfo(
                x.GoalId,
                x.Goal.Title,
                x.Goal.Description ?? string.Empty,
                x.Weight))
            .OrderByDescending(x => x.Weight)
            .ToListAsync(cancellationToken);

        if (goals.Count == 0)
        {
            return false;
        }

        var items = await LoadPathItemsAsync(context, pathId, cancellationToken);
        var existingRows = await context.LearningPathGoalItemMappings
            .Where(x => x.PathId == pathId)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            if (existingRows.Count > 0)
            {
                context.LearningPathGoalItemMappings.RemoveRange(existingRows);
            }

            return true;
        }

        List<NormalizedGoalScores> scoredItems;
        if (goals.Count == 1)
        {
            scoredItems = BuildSingleGoalScores(goals[0], items);
        }
        else
        {
            scoredItems = await BuildAiScoresWithFallbackAsync(
                aiGeneratorService,
                goals,
                items,
                language,
                cancellationToken);
        }

        var now = DateTime.UtcNow;
        var newRows = BuildRows(pathId, scoredItems, now);

        context.LearningPathGoalItemMappings.RemoveRange(existingRows);
        if (newRows.Count > 0)
        {
            await context.LearningPathGoalItemMappings.AddRangeAsync(newRows, cancellationToken);
        }

        return true;
    }

    private static async Task<List<SemanticItem>> LoadPathItemsAsync(
        IApplicationDbContext context,
        Guid pathId,
        CancellationToken cancellationToken)
    {
        var lessonItems = await context.Lessons
            .Where(l => !l.IsDeleted && !l.Chapter.IsDeleted && l.Chapter.PathId == pathId)
            .Select(l => new SemanticItem(
                l.LessonId,
                LearningPathGoalItemType.Lesson,
                l.Title,
                l.Content ?? string.Empty,
                l.Chapter.Title ?? string.Empty))
            .ToListAsync(cancellationToken);

        var taskItems = await context.Tasks
            .Where(t => t.PathId == pathId && !t.IsDeleted)
            .Select(t => new SemanticItem(
                t.TaskId,
                LearningPathGoalItemType.Task,
                t.Title,
                t.Description ?? string.Empty,
                t.Chapter.Title ?? string.Empty))
            .ToListAsync(cancellationToken);

        var quizItems = await context.Quizzes
            .Where(q => !q.IsDeleted
                        && q.LessonId != null
                        && !q.Lesson!.IsDeleted
                        && !q.Lesson.Chapter.IsDeleted
                        && q.Lesson.Chapter.PathId == pathId)
            .Select(q => new SemanticItem(
                q.QuizId,
                LearningPathGoalItemType.Quiz,
                q.Title,
                q.Description ?? string.Empty,
                q.Lesson != null ? q.Lesson.Title : string.Empty))
            .ToListAsync(cancellationToken);

        return lessonItems
            .Concat(taskItems)
            .Concat(quizItems)
            .ToList();
    }

    private static List<NormalizedGoalScores> BuildSingleGoalScores(
        GoalInfo goal,
        IReadOnlyList<SemanticItem> items)
    {
        return items
            .Select(i => new NormalizedGoalScores(
                i.ItemId,
                i.ItemType,
                new List<GoalScore> { new(goal.GoalId, 1m) }))
            .ToList();
    }

    private static async Task<List<NormalizedGoalScores>> BuildAiScoresWithFallbackAsync(
        IAIGeneratorService aiGeneratorService,
        IReadOnlyList<GoalInfo> goals,
        IReadOnlyList<SemanticItem> items,
        LanguageSelection language,
        CancellationToken cancellationToken)
    {
        var resultByItem = new Dictionary<(Guid ItemId, LearningPathGoalItemType ItemType), List<GoalScore>>();
        foreach (var batch in Chunk(items, MaxItemsPerBatch))
        {
            var batchList = batch.ToList();
            var aiScores = await TryScoreBatchByAiAsync(
                aiGeneratorService,
                goals,
                batchList,
                language,
                cancellationToken);

            if (aiScores.Count == 0)
            {
                aiScores = ScoreBatchByHeuristic(goals, batchList);
            }

            foreach (var scored in aiScores)
            {
                resultByItem[(scored.ItemId, scored.ItemType)] = scored.GoalScores;
            }
        }

        // Ensure every item has score set.
        foreach (var item in items)
        {
            var key = (item.ItemId, item.ItemType);
            if (!resultByItem.ContainsKey(key))
            {
                var fallback = ScoreBatchByHeuristic(goals, new[] { item }).First();
                resultByItem[key] = fallback.GoalScores;
            }
        }

        return resultByItem
            .Select(x => new NormalizedGoalScores(x.Key.ItemId, x.Key.ItemType, x.Value))
            .ToList();
    }

    private static async Task<List<NormalizedGoalScores>> TryScoreBatchByAiAsync(
        IAIGeneratorService aiGeneratorService,
        IReadOnlyList<GoalInfo> goals,
        IReadOnlyList<SemanticItem> items,
        LanguageSelection language,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0 || goals.Count == 0)
        {
            return new List<NormalizedGoalScores>();
        }

        var prompt = BuildPrompt(goals, items, language);
        try
        {
            var response = await aiGeneratorService.GenerateStructureAsync<GoalSemanticBatchResponse>(
                prompt,
                AIUsageType.StructureGeneration);
            return ParseAndNormalizeResponse(response, goals, items);
        }
        catch
        {
            return new List<NormalizedGoalScores>();
        }
    }

    private static string BuildPrompt(
        IReadOnlyList<GoalInfo> goals,
        IReadOnlyList<SemanticItem> items,
        LanguageSelection language)
    {
        var languageHint = language switch
        {
            LanguageSelection.VietNamese => "Language is Vietnamese (technical terms may stay English).",
            LanguageSelection.English => "Language is English.",
            _ => "Language can be mixed."
        };

        var goalPayload = goals.Select(g => new
        {
            goalId = g.GoalId,
            title = g.Title,
            description = g.Description,
            weight = g.Weight
        });

        var itemPayload = items.Select(i => new
        {
            itemId = i.ItemId,
            itemType = i.ItemType.ToString(),
            title = i.Title,
            description = i.Description,
            context = i.Context
        });

        var inputJson = JsonSerializer.Serialize(new
        {
            goals = goalPayload,
            items = itemPayload
        });

        return $@"You are ranking how relevant each learning item is to each goal.
{languageHint}

RULES:
- For each item, return score 0..1 for every goalId.
- Scores for the same item MUST sum to 1.0 (or very close).
- If an item mostly belongs to one goal, give that goal higher score.
- Use semantic meaning, not only exact keyword matching.
- Keep all itemId and goalId exactly as provided.

INPUT JSON:
{inputJson}

OUTPUT JSON FORMAT:
{{
  ""mappings"": [
    {{
      ""itemId"": ""guid"",
      ""itemType"": ""Lesson|Task|Quiz"",
      ""goalScores"": [
        {{ ""goalId"": ""guid"", ""score"": 0.73 }},
        {{ ""goalId"": ""guid"", ""score"": 0.27 }}
      ]
    }}
  ]
}}

IMPORTANT:
- Return ONLY valid JSON.
- Do not omit any item from INPUT.
- Do not include markdown or extra text.";
    }

    private static List<NormalizedGoalScores> ParseAndNormalizeResponse(
        GoalSemanticBatchResponse? response,
        IReadOnlyList<GoalInfo> goals,
        IReadOnlyList<SemanticItem> items)
    {
        var goalIdSet = goals.Select(g => g.GoalId).ToHashSet();
        var itemLookup = items.ToDictionary(x => x.ItemId);
        var normalized = new List<NormalizedGoalScores>();

        if (response?.Mappings == null || response.Mappings.Count == 0)
        {
            return normalized;
        }

        foreach (var mapping in response.Mappings)
        {
            if (!Guid.TryParse(mapping.ItemId, out var itemId))
            {
                continue;
            }

            if (!itemLookup.TryGetValue(itemId, out var item))
            {
                continue;
            }

            if (!Enum.TryParse<LearningPathGoalItemType>(mapping.ItemType, true, out var parsedItemType))
            {
                parsedItemType = item.ItemType;
            }

            var rawGoalScores = new List<GoalScore>();
            if (mapping.GoalScores != null)
            {
                foreach (var score in mapping.GoalScores)
                {
                    if (!Guid.TryParse(score.GoalId, out var goalId))
                    {
                        continue;
                    }

                    if (!goalIdSet.Contains(goalId))
                    {
                        continue;
                    }

                    var safeScore = Math.Clamp(score.Score, 0m, 1m);
                    rawGoalScores.Add(new GoalScore(goalId, safeScore));
                }
            }

            var normalizedScores = NormalizeGoalScores(rawGoalScores, goals);
            if (normalizedScores.Count == 0)
            {
                continue;
            }

            normalized.Add(new NormalizedGoalScores(itemId, parsedItemType, normalizedScores));
        }

        return normalized;
    }

    private static List<NormalizedGoalScores> ScoreBatchByHeuristic(
        IReadOnlyList<GoalInfo> goals,
        IEnumerable<SemanticItem> items)
    {
        var result = new List<NormalizedGoalScores>();
        foreach (var item in items)
        {
            var itemText = $"{item.Title} {item.Description} {item.Context}";
            var itemTokens = Tokenize(itemText);

            var rawScores = new List<GoalScore>();
            foreach (var goal in goals)
            {
                var goalTokens = Tokenize($"{goal.Title} {goal.Description}");
                var overlap = itemTokens.Intersect(goalTokens).Count();
                var lexicalScore = goalTokens.Count == 0 ? 0m : (decimal)overlap / goalTokens.Count;
                var score = lexicalScore + (goal.Weight * 0.15m);
                rawScores.Add(new GoalScore(goal.GoalId, Math.Max(0m, score)));
            }

            var normalized = NormalizeGoalScores(rawScores, goals);
            result.Add(new NormalizedGoalScores(item.ItemId, item.ItemType, normalized));
        }

        return result;
    }

    private static List<GoalScore> NormalizeGoalScores(
        IReadOnlyList<GoalScore> rawScores,
        IReadOnlyList<GoalInfo> goals)
    {
        if (goals.Count == 0)
        {
            return new List<GoalScore>();
        }

        var scoreByGoal = goals.ToDictionary(g => g.GoalId, _ => 0m);
        foreach (var raw in rawScores)
        {
            if (scoreByGoal.ContainsKey(raw.GoalId))
            {
                scoreByGoal[raw.GoalId] = Math.Max(0m, raw.Score);
            }
        }

        var sum = scoreByGoal.Values.Sum();
        if (sum <= 0m)
        {
            var weightSum = goals.Sum(g => g.Weight);
            if (weightSum <= 0m)
            {
                var even = Math.Round(1m / goals.Count, 6);
                return goals.Select(g => new GoalScore(g.GoalId, even)).ToList();
            }

            return goals
                .Select(g => new GoalScore(g.GoalId, Math.Round(g.Weight / weightSum, 6)))
                .ToList();
        }

        return scoreByGoal
            .Select(x => new GoalScore(x.Key, Math.Round(x.Value / sum, 6)))
            .ToList();
    }

    private static List<LearningPathGoalItemMapping> BuildRows(
        Guid pathId,
        IReadOnlyList<NormalizedGoalScores> scoredItems,
        DateTime now)
    {
        var rows = new List<LearningPathGoalItemMapping>();
        foreach (var scored in scoredItems)
        {
            var ordered = scored.GoalScores
                .OrderByDescending(x => x.Score)
                .ToList();
            if (ordered.Count == 0)
            {
                continue;
            }

            var filtered = ordered
                .Where(x => x.Score >= MinRelevanceThreshold)
                .ToList();

            if (filtered.Count == 0)
            {
                filtered.Add(ordered[0]);
            }

            foreach (var goalScore in filtered)
            {
                rows.Add(new LearningPathGoalItemMapping
                {
                    MappingId = NewId.NextGuid(),
                    PathId = pathId,
                    GoalId = goalScore.GoalId,
                    ItemId = scored.ItemId,
                    ItemType = scored.ItemType,
                    RelevanceScore = Math.Round(goalScore.Score, 4),
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        return rows;
    }

    private static HashSet<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new HashSet<string>();
        }

        var normalized = Regex.Replace(text.ToLowerInvariant(), @"[\p{P}\p{S}]", " ");
        var tokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 1)
            .ToHashSet();
        return tokens;
    }

    private static IEnumerable<IEnumerable<T>> Chunk<T>(IReadOnlyList<T> source, int size)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        for (var i = 0; i < source.Count; i += size)
        {
            var count = Math.Min(size, source.Count - i);
            var batch = new List<T>(count);
            for (var j = 0; j < count; j++)
            {
                batch.Add(source[i + j]);
            }

            yield return batch;
        }
    }

    private sealed record GoalInfo(Guid GoalId, string Title, string Description, decimal Weight);

    private sealed record SemanticItem(
        Guid ItemId,
        LearningPathGoalItemType ItemType,
        string Title,
        string Description,
        string Context);

    private sealed record GoalScore(Guid GoalId, decimal Score);

    private sealed record NormalizedGoalScores(
        Guid ItemId,
        LearningPathGoalItemType ItemType,
        List<GoalScore> GoalScores);

    private sealed class GoalSemanticBatchResponse
    {
        public List<GoalSemanticBatchItem> Mappings { get; set; } = new();
    }

    private sealed class GoalSemanticBatchItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public List<GoalSemanticScoreItem> GoalScores { get; set; } = new();
    }

    private sealed class GoalSemanticScoreItem
    {
        public string GoalId { get; set; } = string.Empty;
        public decimal Score { get; set; }
    }
}
