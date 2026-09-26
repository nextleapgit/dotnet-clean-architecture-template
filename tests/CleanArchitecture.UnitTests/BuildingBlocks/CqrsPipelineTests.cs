using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.SharedKernel;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CleanArchitecture.UnitTests.BuildingBlocks;

public sealed class CqrsPipelineTests
{
    public sealed record Ping(string Name) : ICommand<string>;

    public sealed class PingHandler : ICommandHandler<Ping, string>
    {
        public int Calls { get; private set; }

        public Task<Result<string>> HandleAsync(Ping command, CancellationToken cancellationToken)
        {
            Calls++;

            return Task.FromResult(Result.Success($"pong {command.Name}"));
        }
    }

    public sealed class PingValidator : AbstractValidator<Ping>
    {
        public PingValidator() => RuleFor(p => p.Name).NotEmpty();
    }

    public sealed record Lookup(int Id) : IQuery<int>;

    public sealed class LookupHandler : IQueryHandler<Lookup, int>
    {
        public Task<Result<int>> HandleAsync(Lookup query, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(query.Id * 2));
    }

    public sealed class LookupValidator : AbstractValidator<Lookup>
    {
        public LookupValidator() => RuleFor(l => l.Id).GreaterThan(0);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddCqrs(typeof(CqrsPipelineTests).Assembly);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task CommandDispatcher_Should_InvokeHandler_WhenCommandIsValid()
    {
        await using ServiceProvider provider = BuildProvider();
        ICommandDispatcher dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        Result<string> result = await dispatcher.DispatchAsync<Ping, string>(
            new Ping("world"),
            TestContext.Current.CancellationToken);

        result.Value.ShouldBe("pong world");
    }

    [Fact]
    public async Task CommandDispatcher_Should_ReturnValidationErrorWithoutInvokingHandler_WhenCommandIsInvalid()
    {
        await using ServiceProvider provider = BuildProvider();
        ICommandDispatcher dispatcher = provider.GetRequiredService<ICommandDispatcher>();

        Result<string> result = await dispatcher.DispatchAsync<Ping, string>(
            new Ping(string.Empty),
            TestContext.Current.CancellationToken);

        ValidationError error = result.Error.ShouldBeOfType<ValidationError>();
        error.Type.ShouldBe(ErrorType.Validation);
        error.Errors.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task QueryDispatcher_Should_ValidateQueriesToo()
    {
        await using ServiceProvider provider = BuildProvider();
        IQueryDispatcher dispatcher = provider.GetRequiredService<IQueryDispatcher>();

        Result<int> valid = await dispatcher.DispatchAsync<Lookup, int>(new Lookup(21), TestContext.Current.CancellationToken);
        Result<int> invalid = await dispatcher.DispatchAsync<Lookup, int>(new Lookup(0), TestContext.Current.CancellationToken);

        valid.Value.ShouldBe(42);
        invalid.Error.ShouldBeOfType<ValidationError>();
    }

    [Fact]
    public void AddCqrs_Should_RegisterHandlersAsScoped()
    {
        var services = new ServiceCollection();
        services.AddCqrs(typeof(CqrsPipelineTests).Assembly);

        services.Where(d => d.ServiceType == typeof(ICommandHandler<Ping, string>))
            .ShouldAllBe(d => d.Lifetime == ServiceLifetime.Scoped);
    }
}
