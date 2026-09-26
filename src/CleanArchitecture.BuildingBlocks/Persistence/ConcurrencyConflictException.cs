namespace CleanArchitecture.BuildingBlocks.Persistence;

/// <summary>
/// Another request changed the same data first. The API answers 409 so the client can retry.
/// </summary>
public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("The data was modified by another request.", innerException);
