using CodeNexus.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Users.Services;

public class DailyReminderTimeInferenceService : IDailyReminderTimeInferenceService
{
    private static readonly TimeSpan DefaultReminderTime = new(20, 0, 0);

    private readonly IApplicationDbContext _context;
    private readonly IDateTimeProvider _dateTimeProvider;

    public DailyReminderTimeInferenceService(IApplicationDbContext context, IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<TimeSpan> InferDailyReminderTimeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var timezone = ResolveVietnamTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(_dateTimeProvider.UtcNow, timezone);
        var sevenDaysAgoLocal = nowLocal.AddDays(-7);
        var sevenDaysAgoUtc = TimeZoneInfo.ConvertTimeToUtc(sevenDaysAgoLocal, timezone);

        var startTimesUtc = await _context.FocusSessions
            .AsNoTracking()
            .Where(x => x.StartTime >= sevenDaysAgoUtc && x.Task.LearningPath.UserId == userId)
            .Select(x => x.StartTime)
            .ToListAsync(cancellationToken);

        var startHours = startTimesUtc
            .Select(x => TimeZoneInfo.ConvertTimeFromUtc(x, timezone).Hour)
            .ToList();

        if (startHours.Count == 0)
        {
            return DefaultReminderTime;
        }

        var mostFrequentHour = startHours
            .GroupBy(x => x)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key)
            .Select(x => x.Key)
            .First();

        return new TimeSpan(mostFrequentHour, 0, 0);
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
    }
}
