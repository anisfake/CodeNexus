namespace CodeNexus.Application.Common.Interfaces;

public interface ITaskVerificationService
{
    Task<VerificationResult> VerifyCodeSubmissionAsync(string taskTitle, string taskDescription, string submittedCode, string? verificationPrompt = null);
    Task<VerificationResult> VerifySummarySubmissionAsync(string taskTitle, string taskDescription, string submittedSummary, string? verificationPrompt = null);
    Task<VerificationResult> VerifyQuizSubmissionAsync(string taskTitle, string taskDescription, string quizQuestionsJson, string submittedAnswersJson);
}

public class VerificationResult
{
    public int Score { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public bool IsPass { get; set; }
}
