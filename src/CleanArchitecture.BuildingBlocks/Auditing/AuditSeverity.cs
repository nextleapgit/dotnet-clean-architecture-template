namespace CleanArchitecture.BuildingBlocks.Auditing;

// Values are persisted; never renumber.
public enum AuditSeverity
{
    Information = 0,
    Warning = 1,
    Critical = 2
}
