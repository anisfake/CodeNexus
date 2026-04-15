using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Enums;
using System.Text.RegularExpressions;

namespace CodeNexus.Infrastructure.Services;

public class TaskVerificationService : ITaskVerificationService
{
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
        if (!LooksLikeCode(submittedCode))
        {
            return new VerificationResult
            {
                Score = 5,
                Feedback = "Nội dung nộp chưa giống mã nguồn hợp lệ cho bài lập trình. Hãy gửi lại đoạn code thực sự (có cú pháp/lệnh rõ ràng) để AI review chính xác.",
                IsPass = false
            };
        }

        var prompt = BuildCodeVerificationPrompt(taskTitle, taskDescription, submittedCode, verificationPrompt);
        return await GetVerificationResultAsync(prompt);
    }

    public async Task<VerificationResult> VerifySummarySubmissionAsync(
        string taskTitle,
        string taskDescription,
        string submittedSummary,
        string? verificationPrompt = null)
    {
        var prompt = BuildSummaryVerificationPrompt(taskTitle, taskDescription, submittedSummary, verificationPrompt);
        return await GetVerificationResultAsync(prompt);
    }

    public async Task<VerificationResult> VerifyQuizSubmissionAsync(
        string taskTitle,
        string taskDescription,
        string quizQuestionsJson,
        string submittedAnswersJson)
    {
        try
        {
            var prompt = BuildQuizVerificationPrompt(taskTitle, taskDescription, quizQuestionsJson, submittedAnswersJson);
            return await GetVerificationResultAsync(prompt);
        }
        catch (Exception)
        {
            return await FallbackQuizVerification(quizQuestionsJson, submittedAnswersJson);
        }
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

    private static bool LooksLikeCode(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length < 8)
        {
            return false;
        }

        if (trimmed.Contains('\n') || trimmed.Contains('\r'))
        {
            return true;
        }

        if (Regex.IsMatch(trimmed, @"\b(function|def|class|public|private|protected|static|const|let|var|return|if|for|while|switch|import|using|namespace)\b", RegexOptions.IgnoreCase))
        {
            return true;
        }

        return Regex.IsMatch(trimmed, @"[;{}()=<>\[\]]");
    }

    private string BuildQuizVerificationPrompt(string taskTitle, string taskDescription, string quizQuestionsJson, string submittedAnswersJson)
    {
        var basePrompt = $@"You are evaluating a quiz submission. 

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
  ""feedback"": ""Bạn trả lời đúng 4/5 câu. Câu 2 sai, đáp án đúng là C.""
}}";

        return basePrompt;
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

    private async Task<VerificationResult> FallbackQuizVerification(string quizQuestionsJson, string submittedAnswersJson)
    {
        try
        {
            var questions = System.Text.Json.JsonSerializer.Deserialize<QuizQuestion[]>(quizQuestionsJson);
            var studentAnswers = System.Text.Json.JsonSerializer.Deserialize<StudentAnswers>(submittedAnswersJson);

            if (questions == null || studentAnswers?.Answers == null)
            {
                return new VerificationResult
                {
                    Score = 0,
                    Feedback = "Không thể đọc được câu hỏi hoặc câu trả lời. Vui lòng thử lại.",
                    IsPass = false
                };
            }

            int correctCount = 0;
            var feedback = new System.Text.StringBuilder("Kết quả chi tiết:\n");

            for (int i = 0; i < Math.Min(questions.Length, studentAnswers.Answers.Length); i++)
            {
                var question = questions[i];
                var studentAnswer = studentAnswers.Answers[i];
                var isCorrect = studentAnswer == question.CorrectAnswer;

                if (isCorrect)
                {
                    correctCount++;
                    feedback.AppendLine($"Câu {i + 1}: ✓ Đúng");
                }
                else
                {
                    var correctOption = question.Options.Length > question.CorrectAnswer
                        ? question.Options[question.CorrectAnswer]
                        : "N/A";
                    feedback.AppendLine($"Câu {i + 1}: ✗ Sai (Đáp án đúng: {correctOption})");
                }
            }

            var score = (int)Math.Round((double)correctCount / questions.Length * 100);
            feedback.AppendLine($"\nTổng kết: {correctCount}/{questions.Length} câu đúng ({score} điểm)");

            return new VerificationResult
            {
                Score = score,
                Feedback = feedback.ToString(),
                IsPass = score >= 70
            };
        }
        catch (Exception)
        {
            return new VerificationResult
            {
                Score = 0,
                Feedback = "Có lỗi xảy ra khi chấm bài quiz. Vui lòng liên hệ hỗ trợ.",
                IsPass = false
            };
        }
    }

    private class QuizQuestion
    {
        public string Question { get; set; } = string.Empty;
        public string[] Options { get; set; } = Array.Empty<string>();
        public int CorrectAnswer { get; set; }
    }

    private class StudentAnswers
    {
        public int[] Answers { get; set; } = Array.Empty<int>();
    }

    private class AIVerificationResponse
    {
        public int Score { get; set; }
        public string Feedback { get; set; } = string.Empty;
    }
}
