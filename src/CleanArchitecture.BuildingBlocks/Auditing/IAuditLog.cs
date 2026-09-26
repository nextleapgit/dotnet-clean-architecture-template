namespace CleanArchitecture.BuildingBlocks.Auditing;

/// <summary>
/// Append-only audit trail. Entries are written in the same unit of work as the change they
/// describe, so they commit or roll back together. Never log secrets or passwords.
/// </summary>
public interface IAuditLog
{
    void Record(AuditRecord record);
}
