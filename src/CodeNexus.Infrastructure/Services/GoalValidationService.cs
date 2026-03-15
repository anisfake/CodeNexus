using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;
using System.Text.RegularExpressions;

namespace CodeNexus.Infrastructure.Services;

public class GoalValidationService : IGoalValidationService
{
    private readonly IAIGeneratorService _aiGeneratorService;

    private static readonly HashSet<string> ProgrammingKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "c#", "csharp", "java", "python", "javascript", "typescript", "php", "ruby", "go", "rust",
        "kotlin", "swift", "c++", "cpp", "scala", "dart", "r", "matlab", "perl", "lua",

        "lập trình", "code", "coding", "developer", "dev", "programmer", "software", "phần mềm",
        "web", "mobile", "app", "application", "ứng dụng", "api", "backend", "frontend", "fullstack",
        "full-stack", "full stack",

        "react", "angular", "vue", "asp.net", "aspnet", ".net", "dotnet", "spring", "django",
        "flask", "express", "node", "nodejs", "laravel", "rails", "flutter", "xamarin",

        "html", "css", "sql", "nosql", "mongodb", "postgresql", "mysql", "redis", "docker",
        "kubernetes", "git", "github", "gitlab", "ci/cd", "devops", "cloud", "aws", "azure", "gcp",

        "algorithm", "thuật toán", "data structure", "cấu trúc dữ liệu", "oop", "design pattern",
        "database", "cơ sở dữ liệu", "testing", "unit test", "integration", "deployment",
        "microservice", "rest", "graphql", "websocket", "authentication", "authorization",

        "học", "learn", "build", "xây dựng", "phát triển", "develop", "tạo", "create",
        "làm", "make", "viết", "write", "code", "debug", "test", "deploy", "triển khai"
    };

    private static readonly HashSet<string> NonProgrammingKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "nấu ăn", "cooking", "thể thao", "sports", "âm nhạc", "music", "du lịch", "travel",
        "yoga", "gym", "fitness", "vẽ", "painting", "nhiếp ảnh", "photography", "làm vườn", "gardening",
        "đọc sách tiểu thuyết", "novel", "phim ảnh", "movie", "game giải trí", "entertainment"
    };

    public GoalValidationService(IAIGeneratorService aiGeneratorService)
    {
        _aiGeneratorService = aiGeneratorService;
    }

    public async Task<bool> IsRelatedToProgrammingAsync(string goalTitle, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(goalTitle))
            return false;

        var normalizedGoal = NormalizeVietnamese(goalTitle.ToLower());

        if (HasObviousProgrammingKeywords(normalizedGoal))
            return true;

        if (HasNonProgrammingKeywords(normalizedGoal))
            return false;

        return await ValidateWithAI(goalTitle, cancellationToken);
    }

    public async Task<bool> IsGoalRelevantToSubjectAsync(
        string goalTitle,
        string? goalDescription,
        string subjectName,
        string? subjectDescription,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(goalTitle) || string.IsNullOrWhiteSpace(subjectName))
            return false;

        try
        {
            var prompt = $@"Determine whether the learning goal is relevant to the specified subject.

Subject: ""{subjectName}""
Subject description: ""{subjectDescription ?? "N/A"}""

Goal title: ""{goalTitle}""
Goal description: ""{goalDescription ?? "N/A"}""

Reply ONLY with 'YES' or 'NO'.
- YES if the goal clearly relates to the subject's concepts, technologies, tools, or outcomes.
- NO if the goal is unrelated or better suited for a different subject.

Answer:";

            var response = await _aiGeneratorService.GenerateContentAsync(prompt, AIUsageType.Verification);
            var answer = response?.Trim().ToUpperInvariant();

            return answer == "YES";
        }
        catch
        {
            return true;
        }
    }

    public async Task<GoalMatchResult> FindBestSystemGoalMatchAsync(
        string goalTitle,
        string? goalDescription,
        string subjectName,
        string? subjectDescription,
        IReadOnlyList<GoalMatchCandidate> systemGoals,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(goalTitle) || string.IsNullOrWhiteSpace(subjectName) || systemGoals.Count == 0)
        {
            return new GoalMatchResult(null, null);
        }

        try
        {
            var goalsText = string.Join("\n", systemGoals.Select(g =>
                $"- {g.GoalId}: {g.Title} | {g.Description ?? "N/A"}"));

            var prompt = $@"You are matching a user's custom learning goal to the closest system-defined goal for the same subject.

Subject: ""{subjectName}""
Subject description: ""{subjectDescription ?? "N/A"}""

User goal title: ""{goalTitle}""
User goal description: ""{goalDescription ?? "N/A"}""

System goals:
{goalsText}

Pick the SINGLE best matching system goal, if any. If none are relevant, respond with NONE.
Reply ONLY in this format:
BEST: <goalId or NONE>
CONFIDENCE: <number between 0 and 1>

Answer:";

            var response = await _aiGeneratorService.GenerateContentAsync(prompt, AIUsageType.Verification);
            if (string.IsNullOrWhiteSpace(response))
                return new GoalMatchResult(null, null);

            var bestMatch = Regex.Match(response, @"BEST:\s*(?<id>[0-9a-fA-F\-]{36}|NONE)", RegexOptions.IgnoreCase);
            var confidenceMatch = Regex.Match(response, @"CONFIDENCE:\s*(?<conf>0(\.\d+)?|1(\.0+)?)", RegexOptions.IgnoreCase);

            if (!bestMatch.Success)
                return new GoalMatchResult(null, null);

            var idValue = bestMatch.Groups["id"].Value.Trim();
            if (idValue.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                return new GoalMatchResult(null, null);

            if (!Guid.TryParse(idValue, out var goalId))
                return new GoalMatchResult(null, null);

            decimal? confidence = null;
            if (confidenceMatch.Success && decimal.TryParse(confidenceMatch.Groups["conf"].Value, out var confValue))
            {
                confidence = confValue;
            }

            return new GoalMatchResult(goalId, confidence);
        }
        catch
        {
            return new GoalMatchResult(null, null);
        }
    }

    private bool HasObviousProgrammingKeywords(string normalizedGoal)
    {
        return ProgrammingKeywords.Any(keyword =>
            normalizedGoal.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private bool HasNonProgrammingKeywords(string normalizedGoal)
    {
        return NonProgrammingKeywords.Any(keyword =>
            normalizedGoal.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> ValidateWithAI(string goalTitle, CancellationToken cancellationToken)
    {
        try
        {
            var prompt = $@"Analyze if this learning goal is related to programming, software development, or computer science.

Goal: ""{goalTitle}""

Reply ONLY with 'YES' or 'NO'.
- YES: if the goal is about learning programming languages, building software, coding skills, web/mobile development, algorithms, databases, DevOps, or any tech/IT career.
- NO: if the goal is about cooking, sports, music, general life goals, hobbies unrelated to technology, etc.

Answer:";

            var response = await _aiGeneratorService.GenerateContentAsync(prompt, AIUsageType.Verification);
            var answer = response?.Trim().ToUpper();

            return answer == "YES";
        }
        catch
        {
            return true;
        }
    }

    private string NormalizeVietnamese(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var normalized = text
            .Replace("á", "a").Replace("à", "a").Replace("ả", "a").Replace("ã", "a").Replace("ạ", "a")
            .Replace("ă", "a").Replace("ắ", "a").Replace("ằ", "a").Replace("ẳ", "a").Replace("ẵ", "a").Replace("ặ", "a")
            .Replace("â", "a").Replace("ấ", "a").Replace("ầ", "a").Replace("ẩ", "a").Replace("ẫ", "a").Replace("ậ", "a")
            .Replace("é", "e").Replace("è", "e").Replace("ẻ", "e").Replace("ẽ", "e").Replace("ẹ", "e")
            .Replace("ê", "e").Replace("ế", "e").Replace("ề", "e").Replace("ể", "e").Replace("ễ", "e").Replace("ệ", "e")
            .Replace("í", "i").Replace("ì", "i").Replace("ỉ", "i").Replace("ĩ", "i").Replace("ị", "i")
            .Replace("ó", "o").Replace("ò", "o").Replace("ỏ", "o").Replace("õ", "o").Replace("ọ", "o")
            .Replace("ô", "o").Replace("ố", "o").Replace("ồ", "o").Replace("ổ", "o").Replace("ỗ", "o").Replace("ộ", "o")
            .Replace("ơ", "o").Replace("ớ", "o").Replace("ờ", "o").Replace("ở", "o").Replace("ỡ", "o").Replace("ợ", "o")
            .Replace("ú", "u").Replace("ù", "u").Replace("ủ", "u").Replace("ũ", "u").Replace("ụ", "u")
            .Replace("ư", "u").Replace("ứ", "u").Replace("ừ", "u").Replace("ử", "u").Replace("ữ", "u").Replace("ự", "u")
            .Replace("ý", "y").Replace("ỳ", "y").Replace("ỷ", "y").Replace("ỹ", "y").Replace("ỵ", "y")
            .Replace("đ", "d");

        return normalized;
    }
}
