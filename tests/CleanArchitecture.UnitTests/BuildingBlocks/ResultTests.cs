using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.UnitTests.BuildingBlocks;

public sealed class ResultTests
{
    [Fact]
    public void Success_Should_CarryNoError() =>
        Result.Success().Error.ShouldBe(Error.None);

    [Fact]
    public void Failure_Should_RejectErrorNone() =>
        Should.Throw<ArgumentException>(() => Result.Failure(Error.None));

    [Fact]
    public void Value_Should_Throw_OnFailure() =>
        Should.Throw<InvalidOperationException>(() => _ = Result.Failure<int>(Error.NullValue).Value);

    [Fact]
    public void TenantId_Should_CompareByValue()
    {
        var value = Guid.NewGuid();

        new TenantId(value).ShouldBe(new TenantId(value));
        TenantId.New().ShouldNotBe(TenantId.New());
    }
}
