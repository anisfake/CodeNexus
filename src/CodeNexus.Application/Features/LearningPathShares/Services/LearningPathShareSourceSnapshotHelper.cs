using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using CodeNexus.Domain.Entities;

namespace CodeNexus.Application.Features.LearningPathShares.Services;

public static class LearningPathShareSourceSnapshotHelper
{
    private const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string CreateSnapshotJson(LearningPath sourcePath)
    {
        var snapshot = new SourceSnapshot(
            CurrentSchemaVersion,
            sourcePath.VersionNumber,
            sourcePath.Chapters
                .Where(c => !c.IsDeleted)
                .OrderBy(c => c.OrderIndex)
                .Select(c => new SourceSnapshotChapter(
                    c.OrderIndex,
                    NormalizeTitle(c.Title),
                    ComputeTitleHash(c.Title),
                    ComputeContentHash(c.Content),
                    c.Lessons
                        .Where(l => !l.IsDeleted)
                        .OrderBy(l => l.OrderIndex)
                        .Select(l => new SourceSnapshotLesson(
                            c.OrderIndex,
                            l.OrderIndex,
                            NormalizeTitle(l.Title),
                            ComputeTitleHash(l.Title),
                            ComputeContentHash(l.Content)))
                        .ToList()))
                .ToList());

        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    public static LearningPathShareUpdateChangeSummaryDto? TryBuildChangeSummary(
        string? sourceSnapshotJson,
        LearningPath latestSourcePath)
    {
        return TryBuildDiff(sourceSnapshotJson, latestSourcePath)?.ChangeSummary;
    }

    public static IReadOnlySet<string>? TryGetContentChangedLessonKeys(
        string? sourceSnapshotJson,
        LearningPath latestSourcePath)
    {
        return TryBuildDiff(sourceSnapshotJson, latestSourcePath)?.ContentChangedLessonKeys;
    }

    private static SourceSnapshotDiff? TryBuildDiff(string? sourceSnapshotJson, LearningPath latestSourcePath)
    {
        var snapshot = DeserializeSnapshot(sourceSnapshotJson);
        if (snapshot == null)
        {
            return null;
        }

        var latestChapters = latestSourcePath.Chapters
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.OrderIndex)
            .ToList();

        var latestChapterByOrder = latestChapters
            .GroupBy(c => c.OrderIndex)
            .ToDictionary(g => g.Key, g => g.First());
        var snapshotChapterByOrder = snapshot.Chapters
            .GroupBy(c => c.OrderIndex)
            .ToDictionary(g => g.Key, g => g.First());

        var addedChapters = latestChapterByOrder.Keys
            .Except(snapshotChapterByOrder.Keys)
            .Select(order => latestChapterByOrder[order].Title)
            .ToList();

        var removedChapters = snapshotChapterByOrder.Keys
            .Except(latestChapterByOrder.Keys)
            .Select(order => snapshotChapterByOrder[order].Title)
            .ToList();

        var updatedChapters = latestChapterByOrder.Keys
            .Intersect(snapshotChapterByOrder.Keys)
            .Where(order =>
            {
                var latest = latestChapterByOrder[order];
                var baseline = snapshotChapterByOrder[order];
                return !TitleEquals(latest.Title, baseline.Title)
                       || !string.Equals(ComputeContentHash(latest.Content), baseline.ContentHash, StringComparison.Ordinal);
            })
            .Select(order => latestChapterByOrder[order].Title)
            .ToList();

        var latestLessons = latestChapters
            .SelectMany(c => c.Lessons.Where(l => !l.IsDeleted)
                .Select(l => new LessonSnapshotComparisonItem(
                    BuildLessonKey(c.OrderIndex, l.OrderIndex),
                    $"{c.Title} > {l.Title}",
                    l.Title,
                    ComputeTitleHash(l.Title),
                    ComputeContentHash(l.Content))))
            .ToList();

        var baselineLessons = snapshot.Chapters
            .SelectMany(c => c.Lessons
                .Select(l => new LessonSnapshotComparisonItem(
                    BuildLessonKey(c.OrderIndex, l.OrderIndex),
                    $"{c.Title} > {l.Title}",
                    l.Title,
                    l.TitleHash,
                    l.ContentHash)))
            .ToList();

        var latestLessonMap = latestLessons
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.First());
        var baselineLessonMap = baselineLessons
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.First());

        var addedLessons = latestLessonMap.Keys
            .Except(baselineLessonMap.Keys)
            .Select(key => latestLessonMap[key].Label)
            .ToList();

        var removedLessons = baselineLessonMap.Keys
            .Except(latestLessonMap.Keys)
            .Select(key => baselineLessonMap[key].Label)
            .ToList();

        var updatedLessonKeys = latestLessonMap.Keys
            .Intersect(baselineLessonMap.Keys)
            .Where(key =>
            {
                var latest = latestLessonMap[key];
                var baseline = baselineLessonMap[key];
                return !TitleEquals(latest.Title, baseline.Title)
                       || !string.Equals(latest.ContentHash, baseline.ContentHash, StringComparison.Ordinal);
            })
            .ToList();

        var contentChangedLessonKeys = latestLessonMap.Keys
            .Intersect(baselineLessonMap.Keys)
            .Where(key => !string.Equals(
                latestLessonMap[key].ContentHash,
                baselineLessonMap[key].ContentHash,
                StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        return new SourceSnapshotDiff(
            new LearningPathShareUpdateChangeSummaryDto(
                addedChapters.Count,
                removedChapters.Count,
                updatedChapters.Count,
                addedLessons.Count,
                removedLessons.Count,
                updatedLessonKeys.Count,
                SortDistinct(addedChapters),
                SortDistinct(removedChapters),
                SortDistinct(updatedChapters),
                SortDistinct(addedLessons),
                SortDistinct(removedLessons),
                SortDistinct(updatedLessonKeys.Select(key => latestLessonMap[key].Label).ToList())),
            contentChangedLessonKeys);
    }

    private static SourceSnapshot? DeserializeSnapshot(string? sourceSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(sourceSnapshotJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SourceSnapshot>(sourceSnapshotJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string BuildLessonKey(int chapterOrderIndex, int lessonOrderIndex)
        => $"{chapterOrderIndex}|{lessonOrderIndex}";

    private static bool TitleEquals(string? left, string? right)
        => string.Equals(NormalizeTitle(left), NormalizeTitle(right), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeTitle(string? text)
        => string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();

    private static string NormalizeContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        return content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();
    }

    private static string ComputeTitleHash(string? title)
        => ComputeHash(NormalizeTitle(title).ToUpperInvariant());

    private static string ComputeContentHash(string? content)
        => ComputeHash(NormalizeContent(content));

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private static List<string> SortDistinct(List<string> items)
        => items
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private sealed record SourceSnapshot(
        int SchemaVersion,
        decimal SourceVersion,
        List<SourceSnapshotChapter> Chapters);

    private sealed record SourceSnapshotChapter(
        int OrderIndex,
        string Title,
        string TitleHash,
        string ContentHash,
        List<SourceSnapshotLesson> Lessons);

    private sealed record SourceSnapshotLesson(
        int ChapterOrderIndex,
        int OrderIndex,
        string Title,
        string TitleHash,
        string ContentHash);

    private sealed record LessonSnapshotComparisonItem(
        string Key,
        string Label,
        string Title,
        string TitleHash,
        string ContentHash);

    private sealed record SourceSnapshotDiff(
        LearningPathShareUpdateChangeSummaryDto ChangeSummary,
        IReadOnlySet<string> ContentChangedLessonKeys);
}
