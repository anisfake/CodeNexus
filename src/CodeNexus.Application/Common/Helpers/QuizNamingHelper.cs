using System.Text.RegularExpressions;
using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Common.Helpers;

public static class QuizNamingHelper
{
    private static readonly Regex NonWordRegex = new(@"[\p{P}\p{S}]", RegexOptions.Compiled);
    private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);

    public static string EnsureRelatedTitle(string? proposedTitle, string lessonTitle, int quizIndex, LanguageSelection language)
    {
        var cleanTitle = (proposedTitle ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cleanTitle))
        {
            return BuildFallbackTitle(lessonTitle, quizIndex, language);
        }

        if (IsEquivalent(cleanTitle, lessonTitle) || !IsRelatedToLesson(cleanTitle, lessonTitle))
        {
            return BuildFallbackTitle(lessonTitle, quizIndex, language);
        }

        return cleanTitle;
    }

    public static IReadOnlyList<string> BuildFinalTitles(
        IEnumerable<string?>? aiTitles,
        string lessonTitle,
        int quizCount,
        LanguageSelection language)
    {
        var source = aiTitles?.ToList() ?? new List<string?>();
        var result = new List<string>(quizCount);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < quizCount; i++)
        {
            var title = EnsureRelatedTitle(i < source.Count ? source[i] : null, lessonTitle, i, language);
            title = EnsureDistinct(title, used);
            result.Add(title);
        }

        return result;
    }

    public static string BuildFallbackTitle(string lessonTitle, int quizIndex, LanguageSelection language)
    {
        var safeLessonTitle = string.IsNullOrWhiteSpace(lessonTitle)
            ? (language == LanguageSelection.VietNamese ? "Bài học hiện tại" : "Current lesson")
            : lessonTitle.Trim();

        return language switch
        {
            LanguageSelection.VietNamese => (quizIndex % 3) switch
            {
                0 => $"Kiểm tra nhanh: {safeLessonTitle}",
                1 => $"Bài tập ứng dụng: {safeLessonTitle}",
                _ => $"Tổng hợp trọng tâm: {safeLessonTitle}"
            },
            _ => (quizIndex % 3) switch
            {
                0 => $"Quick Check: {safeLessonTitle}",
                1 => $"Applied Practice: {safeLessonTitle}",
                _ => $"Key Concept Review: {safeLessonTitle}"
            }
        };
    }

    public static string BuildFallbackDescription(string lessonTitle, int quizIndex, LanguageSelection language)
    {
        var safeLessonTitle = string.IsNullOrWhiteSpace(lessonTitle)
            ? (language == LanguageSelection.VietNamese ? "bài học này" : "this lesson")
            : lessonTitle.Trim();

        return language switch
        {
            LanguageSelection.VietNamese => (quizIndex % 3) switch
            {
                0 => $"Bài quiz kiểm tra mức độ hiểu các ý chính trong \"{safeLessonTitle}\".",
                1 => $"Bài quiz tập trung vào khả năng áp dụng kiến thức từ \"{safeLessonTitle}\" vào tình huống thực tế.",
                _ => $"Bài quiz ôn tập các điểm trọng tâm và cách triển khai từ \"{safeLessonTitle}\"."
            },
            _ => (quizIndex % 3) switch
            {
                0 => $"This quiz checks your understanding of the core ideas in \"{safeLessonTitle}\".",
                1 => $"This quiz focuses on applying concepts from \"{safeLessonTitle}\" to practical scenarios.",
                _ => $"This quiz reviews the key takeaways and implementation details from \"{safeLessonTitle}\"."
            }
        };
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = NonWordRegex.Replace(value.Trim().ToLowerInvariant(), " ");
        normalized = MultiSpaceRegex.Replace(normalized, " ");
        return normalized.Trim();
    }

    private static bool IsEquivalent(string a, string b)
        => Normalize(a) == Normalize(b);

    private static bool IsRelatedToLesson(string quizTitle, string lessonTitle)
    {
        var normalizedQuiz = Normalize(quizTitle);
        var normalizedLesson = Normalize(lessonTitle);
        if (string.IsNullOrWhiteSpace(normalizedQuiz) || string.IsNullOrWhiteSpace(normalizedLesson))
        {
            return false;
        }

        if (normalizedQuiz.Contains(normalizedLesson, StringComparison.OrdinalIgnoreCase) ||
            normalizedLesson.Contains(normalizedQuiz, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var quizTokens = normalizedQuiz.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lessonTokens = normalizedLesson
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return quizTokens.Any(t => t.Length > 2 && lessonTokens.Contains(t));
    }

    private static string EnsureDistinct(string title, ISet<string> used)
    {
        var candidate = title;
        var seed = title;
        var suffix = 2;
        while (!used.Add(Normalize(candidate)))
        {
            candidate = $"{seed} ({suffix++})";
        }

        return candidate;
    }
}
