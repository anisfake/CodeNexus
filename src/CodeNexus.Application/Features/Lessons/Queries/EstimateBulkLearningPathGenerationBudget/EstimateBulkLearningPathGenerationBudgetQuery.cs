using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.Lessons.Queries.EstimateBulkLearningPathGenerationBudget;

public record EstimateBulkLearningPathGenerationBudgetQuery(
    int PendingLessonCount,
    int PendingQuizCount) : IRequest<Result<EstimateBulkLearningPathGenerationBudgetDto>>;

public sealed record EstimateBulkLearningPathGenerationBudgetDto(
    bool IsValidationApplied,
    bool IsEnoughTokenBalance,
    decimal EstimatedRequiredTokens,
    decimal CurrentTokenBalance,
    int PendingLessonCount,
    int PendingQuizCount,
    int EstimatedAiCalls);
