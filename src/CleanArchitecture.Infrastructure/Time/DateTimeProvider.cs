using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.Infrastructure.Time;

internal sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
