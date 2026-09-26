namespace CleanArchitecture.Infrastructure.Email;

internal static class UtcTimestamps
{
    public static DateTime ToWholeMilliseconds(DateTime value)
    {
        DateTime utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc);

        return new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    }
}
