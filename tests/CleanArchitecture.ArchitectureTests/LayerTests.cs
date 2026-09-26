namespace CleanArchitecture.ArchitectureTests;

/// <summary>One-way dependencies: Domain → nothing; Application → Domain; Infrastructure → Application; Api → composition.</summary>
public sealed class LayerTests : BaseTest
{
    [Fact]
    public void SharedKernel_Should_NotDependOnAnyOtherProject() =>
        ShouldPass(Types.InAssembly(SharedKernelAssembly).Should()
            .NotHaveDependencyOnAny(
                Name(BuildingBlocksAssembly),
                Name(DomainAssembly),
                Name(ApplicationAssembly),
                Name(InfrastructureAssembly),
                Name(ApiAssembly))
            .GetResult());

    [Fact]
    public void BuildingBlocks_Should_DependOnlyOnSharedKernel() =>
        ShouldPass(Types.InAssembly(BuildingBlocksAssembly).Should()
            .NotHaveDependencyOnAny(
                Name(DomainAssembly),
                Name(ApplicationAssembly),
                Name(InfrastructureAssembly),
                Name(ApiAssembly))
            .GetResult());

    [Fact]
    public void Domain_Should_NotDependOnApplicationInfrastructureOrApi() =>
        ShouldPass(Types.InAssembly(DomainAssembly).Should()
            .NotHaveDependencyOnAny(
                Name(BuildingBlocksAssembly),
                Name(ApplicationAssembly),
                Name(InfrastructureAssembly),
                Name(ApiAssembly))
            .GetResult());

    [Fact]
    public void Application_Should_NotDependOnInfrastructureOrApi() =>
        ShouldPass(Types.InAssembly(ApplicationAssembly).Should()
            .NotHaveDependencyOnAny(Name(InfrastructureAssembly), Name(ApiAssembly))
            .GetResult());

    [Fact]
    public void Infrastructure_Should_NotDependOnApi() =>
        ShouldPass(Types.InAssembly(InfrastructureAssembly).Should()
            .NotHaveDependencyOn(Name(ApiAssembly))
            .GetResult());

    [Fact]
    public void ApiEndpoints_Should_NotAccessTheDatabaseDirectly() =>
        ShouldPass(Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace("CleanArchitecture.Api.Endpoints")
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "CleanArchitecture.Infrastructure.Database",
                "CleanArchitecture.Application.Todos.ITodoItemStore",
                "CleanArchitecture.Application.Users.IUserStore")
            .GetResult());
}
