using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Quizzes.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Quizzes.Queries.GetQuizStatus;

public class GetQuizStatusQueryHandler : IRequestHandler<GetQuizStatusQuery, Result<QuizStatusDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IQuizCacheService _quizCacheService;

    public GetQuizStatusQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IQuizCacheService quizCacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _quizCacheService = quizCacheService;
    }

    public async Task<Result<QuizStatusDto>> Handle(GetQuizStatusQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var cachedStatus = await _quizCacheService.GetQuizStatusAsync(request.QuizId, userId, cancellationToken);
        if (cachedStatus != null)
        {
            return Result<QuizStatusDto>.Success(cachedStatus);
        }

        var quiz = await _context.Quizzes
                .AsNoTracking()
                .Include(q => q.Lesson)
                    .ThenInclude(l => l!.Chapter)
                        .ThenInclude(c => c.LearningPath)
                .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.QuizId == request.QuizId && !q.IsDeleted, cancellationToken);

        if (quiz == null)
            return Result<QuizStatusDto>.Failure("QUIZ_NOT_FOUND", "Quiz not found");

        if (quiz.Lesson == null)
            return Result<QuizStatusDto>.Failure("QUIZ_NO_LESSON", "Quiz is not associated with a lesson");

        if (quiz.Lesson.Chapter.LearningPath.UserId != userId)
            return Result<QuizStatusDto>.Failure("UNAUTHORIZED", "You do not have access to this quiz");

        var lastAttempt = await _context.QuizAttempts
            .AsNoTracking()
            .Where(a => a.QuizId == request.QuizId && a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        string status;
        if (lastAttempt == null)
        {
            status = "NotStarted";
        }
        else
        {
            status = lastAttempt.Status switch
            {
                QuizAttemptStatus.InProgress => "InProgress",
                QuizAttemptStatus.Passed => "Passed",
                QuizAttemptStatus.NotPassed => "NotPassed",
                _ => "NotStarted"
            };
        }

        var statusDto = new QuizStatusDto(
            quiz.QuizId,
            quiz.Title,
            quiz.TimeLimit,
            quiz.PassingScore,
            quiz.Questions.Count,
            status,
            lastAttempt?.AttemptId,
            lastAttempt?.Score,
            lastAttempt?.EndTime
        );

        await _quizCacheService.SetQuizStatusAsync(request.QuizId, userId, statusDto, TimeSpan.FromMinutes(2), cancellationToken);

        return Result<QuizStatusDto>.Success(statusDto);
    }
}
