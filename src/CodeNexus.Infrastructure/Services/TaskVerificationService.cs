using CodeNexus.Application.Common.Interfaces;

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
        var prompt = BuildQuizVerificationPrompt(taskTitle, taskDescription, quizQuestionsJson, submittedAnswersJson);
        return await GetVerificationResultAsync(prompt);
    }

    private string BuildCodeVerificationPrompt(string taskTitle, string taskDescription, string submittedCode, string? customPrompt)
    {
        var basePrompt = $@"You are a programming instructor evaluating a student's code submission.

Task Title: {taskTitle}
Task Description: {taskDescription}

Student's Submitted Code:
```
{submittedCode}
```

{(string.IsNullOrEmpty(customPrompt) ? "" : $"Additional Verification Criteria:\n{customPrompt}\n")}

Evaluate the code based on:
1. Does it address the task requirements?
2. Is the code functional and correct?
3. Code quality and best practices
4. Completeness of the solution

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

{(string.IsNullOrEmpty(customPrompt) ? "" : $"Additional Verification Criteria:\n{customPrompt}\n")}

Evaluate the summary based on:
1. Understanding of key concepts
2. Accuracy of information
3. Completeness of coverage
4. Clarity of explanation

Provide a score from 0-100 and constructive feedback in Vietnamese.

Respond in JSON format:
{{
  ""score"": <number 0-100>,
  ""feedback"": ""<your detailed feedback in Vietnamese>""
}}";

        return basePrompt;
    }

    private string BuildQuizVerificationPrompt(string taskTitle, string taskDescription, string quizQuestionsJson, string submittedAnswersJson)
    {
        var basePrompt = $@"You are an instructor evaluating a student's quiz submission.

Task Title: {taskTitle}
Task Description: {taskDescription}

Quiz Questions (JSON format):
{quizQuestionsJson}

Student's Submitted Answers (JSON format):
{submittedAnswersJson}

Evaluate the quiz submission by:
1. Parsing the quiz questions and correct answers
2. Comparing student's answers with correct answers
3. Calculating the percentage of correct answers
4. Providing constructive feedback in Vietnamese

The quiz questions are in this format:
{{
  ""question"": ""Question text"",
  ""options"": [""Option A"", ""Option B"", ""Option C"", ""Option D""],
  ""correctAnswer"": <index 0-3>
}}

The student answers should be in this format:
{{
  ""answers"": [0, 1, 2, 1, 3]  // Array of selected option indices
}}

Calculate score as: (correct answers / total questions) * 100

Respond in JSON format:
{{
  ""score"": <number 0-100>,
  ""feedback"": ""<detailed feedback in Vietnamese showing which questions were correct/incorrect>""
}}";

        return basePrompt;
    }

    private async Task<VerificationResult> GetVerificationResultAsync(string prompt)
    {
        try
        {
            var response = await _aiService.GenerateStructureAsync<AIVerificationResponse>(prompt);
            
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

    private class AIVerificationResponse
    {
        public int Score { get; set; }
        public string Feedback { get; set; } = string.Empty;
    }
}
