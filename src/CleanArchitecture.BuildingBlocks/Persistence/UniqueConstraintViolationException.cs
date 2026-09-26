namespace CleanArchitecture.BuildingBlocks.Persistence;

/// <summary>
/// A concurrent request inserted the same unique value first (e.g. two sign-ups with one email).
/// Use cases still check uniqueness up front; this covers the race between check and insert.
/// The API answers 409.
/// </summary>
public sealed class UniqueConstraintViolationException(Exception innerException)
    : Exception("A unique constraint was violated.", innerException);
