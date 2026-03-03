using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Dashboard.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Dashboard.Queries.GetStudentDashboardStats;

public class GetStudentDashboardStatsQueryHandler
    : IRequestHandler<GetStudentDashboardStatsQuery, Result<StudentDashboardStatsResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetStudentDashboardStatsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<StudentDashboardStatsResponse>> Handle(
        GetStudentDashboardStatsQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var user = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.UserId == userId, cancellationToken);

        if (!user)
            return Result<StudentDashboardStatsResponse>.Failure("USER_NOT_FOUND", "User not found.");

        var chapters = await _context.Chapters
            .AsNoTracking()
            .Where(c => c.LearningPath.UserId == userId)
            .Select(c => new
            {
                c.ChapterId,
                c.IsCompleted,
                LessonCount = c.Lessons.Count()
            })
            .ToListAsync(cancellationToken);

        var totalChapters = chapters.Count;
        var completedChapters = chapters.Count(c => c.IsCompleted);
        var totalLessons = chapters.Sum(c => c.LessonCount);
        var completedLessons = chapters.Where(c => c.IsCompleted).Sum(c => c.LessonCount);

        var totalLearningPaths = await _context.LearningPaths
            .AsNoTracking()
            .CountAsync(lp => lp.UserId == userId, cancellationToken);

        var totalQuizAttempts = await _context.QuizAttempts
            .AsNoTracking()
            .CountAsync(qa => qa.UserId == userId, cancellationToken);

        var totalStudyMinutes = await _context.FocusSessions
            .AsNoTracking()
            .Where(fs => fs.Task.LearningPath.UserId == userId)
            .SumAsync(fs => fs.DurationInMinutes, cancellationToken);

        var checkinDates = await _context.DailyCheckins
            .AsNoTracking()
            .Where(dc => dc.FocusSession.Task.LearningPath.UserId == userId)
            .Select(dc => dc.CheckinDate.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync(cancellationToken);

        var currentStreak = CalculateStreak(checkinDates, DateTime.Today);

        var response = new StudentDashboardStatsResponse(
            TotalLessons: totalLessons,
            CompletedLessons: completedLessons,
            TotalChapters: totalChapters,
            CompletedChapters: completedChapters,
            TotalLearningPaths: totalLearningPaths,
            TotalQuizAttempts: totalQuizAttempts,
            TotalStudyMinutes: totalStudyMinutes,
            CurrentStreak: currentStreak
        );

        return Result<StudentDashboardStatsResponse>.Success(response);
    }

    public static int CalculateStreak(List<DateTime> sortedDatesDesc, DateTime today)
    {
        if (sortedDatesDesc.Count == 0)
            return 0;

        var latest = sortedDatesDesc[0];
        if (latest != today && latest != today.AddDays(-1))
            return 0;

        var streak = 0;
        var expectedDate = latest;

        foreach (var date in sortedDatesDesc)
        {
            if (date == expectedDate)
            {
                streak++;
                expectedDate = expectedDate.AddDays(-1);
            }
            else
            {
                break;
            }
        }

        return streak;
    }
}
