using CleanArchitecture.BuildingBlocks.Auditing;

namespace CleanArchitecture.UnitTests.Fakes;

public sealed class RecordingAuditLog : IAuditLog
{
    public List<AuditRecord> Records { get; } = [];

    public void Record(AuditRecord record) => Records.Add(record);
}
