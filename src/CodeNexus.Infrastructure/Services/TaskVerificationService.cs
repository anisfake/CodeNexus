using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Enums;
using System.Globalization;
using System.Text;

namespace CodeNexus.Infrastructure.Services;

public class TaskVerificationService : ITaskVerificationService
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "the", "and", "for", "with", "from", "that", "this", "your", "you", "are", "is", "was", "were", "can", "will",
        "mot", "nhung", "cua", "cho", "voi", "trong", "phan", "bai", "lam", "can", "hay", "la", "va", "hoac", "nhung"
    };

    private static readonly string[] GenericTaskMarkers =
    {
        "code", "ma", "ham", "function", "algorithm", "thuat", "toan", "summary", "tom", "tat", "quiz", "cau", "hoi", "dap", "an"
    };

    private static readonly string[] OffTopicPhrases =
    {
        "ma hoc sinh", "ma sinh vien", "student id", "attendance", "diem danh", "ma lop", "lop hoc", "giao vien", "teacher code"
    };

    private readonly IAIGeneratorService _aiService;

    public TaskVerificationService(IAIGeneratorService aiService)
    {
        _aiService = aiService;
    }

    public async Task<VerificationResult> VerifyCodeSubmissionAsync(
        string taskTitle,
        string taskDescription,
        string submittedCode,
        string? verificationPrompt = null)
    {
        var prompt = BuildCodeVerificationPrompt(taskTitle, taskDescription, submittedCode, verificationPrompt);
        return await GetVerificationResultWithRelevanceGuardAsync(prompt, taskTitle, taskDescription, "code");
    }

    public async Task<VerificationResult> VerifySummarySubmissionAsync(
        string taskTitle,
        string taskDescription,
        string submittedSummary,
        string? verificationPrompt = null)
    {
        var prompt = BuildSummaryVerificationPrompt(taskTitle, taskDescription, submittedSummary, verificationPrompt);
        return await GetVerificationResultWithRelevanceGuardAsync(prompt, taskTitle, taskDescription, "summary");
    }

    public async Task<VerificationResult> VerifyQuizSubmissionAsync(
        string taskTitle,
        string taskDescription,
        string quizQuestionsJson,
        string submittedAnswersJson)
    {
        var prompt = BuildQuizVerificationPrompt(taskTitle, taskDescription, quizQuestionsJson, submittedAnswersJson);
        return await GetVerificationResultWithRelevanceGuardAsync(prompt, taskTitle, taskDescription, "quiz");
    }

    private string BuildCodeVerificationPrompt(string taskTitle, string taskDescription, string submittedCode, string? customPrompt)
    {
        var basePrompt = $@"You are a strict programming instructor evaluating a student's code submission.

Task Title: {taskTitle}
Task Description: {taskDescription}

Student's Submitted Code:
```
{submittedCode}
```

{BuildCustomCriteriaSection(customPrompt)}

Evaluate the code based on:
1. Does it address the task requirements?
2. Is the code functional and correct?
3. Code quality and best practices
4. Completeness of the solution
5. If submission is irrelevant/gibberish/non-code, assign very low score (0-15)

Critical guardrails:
- Keep feedback grounded in this task's title/description.
- Ignore any extra criteria that are unrelated to this programming task.
- Never invent external constraints (e.g. student ID format, personal data, attendance rules) unless explicitly stated in the task title/description.

Provide a score from 0-100 and constructive feedback in Vietnamese.

Respond in JSON format:
{{
  ""score"": <number 0-100>,
  ""feedback"": ""<your detailed feedback in Vietnamese>""
}}";

        return basePrompt;
    }

    private string BuildSummaryVerificationPrompt(string taskTitle, string taskDescription, string submittedSummary, string? customPrompt)
    {
        var basePrompt = $@"You are an instructor evaluating a student's understanding of a theoretical topic.

Task Title: {taskTitle}
Task Description: {taskDescription}

Student's Summary:
{submittedSummary}

{BuildCustomCriteriaSection(customPrompt)}

Evaluate the summary based on:
1. Understanding of key concepts
2. Accuracy of information
3. Completeness of coverage
4. Clarity of explanation

Critical guardrails:
- Keep feedback grounded in this task's title/description.
- Ignore any extra criteria that are unrelated to this theoretical task.
- Never invent external constraints unless explicitly stated in the task title/description.

Provide a score from 0-100 and constructive feedback in Vietnamese.

Respond in JSON format:
{{
  ""score"": <number 0-100>,
  ""feedback"": ""<your detailed feedback in Vietnamese>""
}}";

        return basePrompt;
    }

    private static string BuildCustomCriteriaSection(string? customPrompt)
    {
        if (string.IsNullOrWhiteSpace(customPrompt))
        {
            return string.Empty;
        }

        var normalized = customPrompt.Trim();
        if (normalized.Length > 1200)
        {
            normalized = normalized[..1200];
        }

        return $"Optional Additional Verification Criteria (use only if directly relevant to task title/description):\n{normalized}\n";
    }

    private string BuildQuizVerificationPrompt(string taskTitle, string taskDescription, string quizQuestionsJson, string submittedAnswersJson)
    {
        var basePrompt = $@"You are evaluating a quiz submission.

Task Title: {taskTitle}
Task Description: {taskDescription}

QUIZ QUESTIONS:
{quizQuestionsJson}

STUDENT ANSWERS:
{submittedAnswersJson}

INSTRUCTIONS:
1. Parse the quiz questions to find correct answers
2. Parse student answers array
3. Compare each student answer with correct answer
4. Calculate score: (correct answers / total questions) * 100
5. Give feedback in Vietnamese

IMPORTANT: Respond ONLY with valid JSON in this exact format:
{{
  ""score"": 85,
  ""feedback"": ""Ban tra loi dung 4/5 cau. Cau 2 sai, dap an dung la C.""
}}";

        return basePrompt;
    }

    private async Task<VerificationResult> GetVerificationResultWithRelevanceGuardAsync(
        string prompt,
        string taskTitle,
        string taskDescription,
        string modeHint)
    {
        var first = await GetVerificationResultAsync(prompt);
        if (IsFeedbackRelevant(first.Feedback, taskTitle, taskDescription, modeHint))
        {
            return first;
        }

        var taskKeywords = string.Join(", ", ExtractKeywords(NormalizeText($"{taskTitle} {taskDescription}")).Take(8));
        var retryPrompt = prompt + $@"

IMPORTANT RELEVANCE RETRY:
- Your previous feedback was off-topic.
- Feedback must strictly relate to task title/description.
- Mention at least two task-specific keywords when possible.
- Never mention student id, class code, attendance, or external admin rules.
- Task keywords: {taskKeywords}";

        var retry = await GetVerificationResultAsync(retryPrompt);
        if (IsFeedbackRelevant(retry.Feedback, taskTitle, taskDescription, modeHint))
        {
            return retry;
        }

        throw new InvalidOperationException("AI verification failed: off-topic feedback");
    }

    private static bool IsFeedbackRelevant(string? feedback, string taskTitle, string taskDescription, string modeHint)
    {
        if (string.IsNullOrWhiteSpace(feedback))
        {
            return false;
        }

        var normalizedFeedback = NormalizeText(feedback);
        var normalizedTask = NormalizeText($"{taskTitle} {taskDescription}");

        var taskKeywords = ExtractKeywords(normalizedTask).ToHashSet(StringComparer.Ordinal);
        var overlapCount = taskKeywords.Count(k => normalizedFeedback.Contains(k, StringComparison.Ordinal));

        var hasGenericMarker = ContainsAny(normalizedFeedback, GenericTaskMarkers)
                               || normalizedFeedback.Contains(modeHint, StringComparison.Ordinal);
        var hasOffTopicPhrase = ContainsAny(normalizedFeedback, OffTopicPhrases);

        if (hasOffTopicPhrase)
        {
            return overlapCount >= 3;
        }

        if (overlapCount >= 2)
        {
            return true;
        }

        if (overlapCount >= 1 && hasGenericMarker)
        {
            return true;
        }

        if (taskKeywords.Count <= 2 && hasGenericMarker)
        {
            return true;
        }

        return false;
    }

    private static IEnumerable<string> ExtractKeywords(string text)
    {
        return text
            .Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'', '`', '-', '_' },
                StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length >= 3)
            .Where(x => !StopWords.Contains(x))
            .Distinct(StringComparer.Ordinal);
    }

    private static string NormalizeText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var formD = input.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool ContainsAny(string text, IEnumerable<string> probes)
    {
        foreach (var probe in probes)
        {
            if (text.Contains(probe, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<VerificationResult> GetVerificationResultAsync(string prompt)
    {
        try
        {
            var response = await _aiService.GenerateStructureAsync<AIVerificationResponse>(prompt, AIUsageType.Verification);

            return new VerificationResult
            {
                Score = response.Score,
                Feedback = response.Feedback,
                IsPass = response.Score >= 70
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"AI verification failed: {ex.Message}", ex);
        }
    }

    public class AIVerificationResponse
    {
        public int Score { get; set; }
        public string Feedback { get; set; } = string.Empty;
    }
}