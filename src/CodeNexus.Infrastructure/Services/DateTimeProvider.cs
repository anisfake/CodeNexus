using CodeNexus.Application.Common.Interfaces;

namespace CodeNexus.Infrastructure.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
