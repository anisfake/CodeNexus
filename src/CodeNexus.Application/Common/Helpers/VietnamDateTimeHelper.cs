namespace CodeNexus.Application.Common.Helpers;

public static class VietnamDateTimeHelper
{
    private static readonly TimeZoneInfo? VietnamTimeZone = ResolveVietnamTimeZone();

    public static DateTime GetTodayDate()
    {
        return GetTodayDate(DateTime.UtcNow);
    }

    public static DateTime GetTodayDate(DateTime utcNow)
    {
        var normalizedUtc = utcNow.Kind switch
        {
            DateTimeKind.Utc => utcNow,
            DateTimeKind.Local => utcNow.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)
        };

        if (VietnamTimeZone != null)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc, VietnamTimeZone).Date;
        }

        return normalizedUtc.AddHours(7).Date;
    }

    private static TimeZoneInfo? ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch (TimeZoneNotFoundException)
            {
                return null;
            }
            catch (InvalidTimeZoneException)
            {
                return null;
            }
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
