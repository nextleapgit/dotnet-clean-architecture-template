namespace CleanArchitecture.Infrastructure.Email;

// Persistent contract: the values are referenced by check constraints, partial indexes, and raw SQL.
internal enum EmailOutboxStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3,
    Expired = 4
}
