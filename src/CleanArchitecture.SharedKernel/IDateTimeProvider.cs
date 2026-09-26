namespace CleanArchitecture.SharedKernel;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
