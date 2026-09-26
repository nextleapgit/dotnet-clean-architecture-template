using System.Reflection;
using CleanArchitecture.Api;
using CleanArchitecture.BuildingBlocks.Cqrs;
using CleanArchitecture.Domain.Users;
using CleanArchitecture.Infrastructure.Database;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.ArchitectureTests;

public abstract class BaseTest
{
    protected static readonly Assembly SharedKernelAssembly = typeof(Entity).Assembly;
    protected static readonly Assembly BuildingBlocksAssembly = typeof(ICommand).Assembly;
    protected static readonly Assembly DomainAssembly = typeof(User).Assembly;
    protected static readonly Assembly ApplicationAssembly = typeof(CleanArchitecture.Application.DependencyInjection).Assembly;
    protected static readonly Assembly InfrastructureAssembly = typeof(ApplicationDbContext).Assembly;
    protected static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    protected static readonly Assembly[] AllAssemblies =
    [
        SharedKernelAssembly,
        BuildingBlocksAssembly,
        DomainAssembly,
        ApplicationAssembly,
        InfrastructureAssembly,
        ApiAssembly
    ];

    protected static string Name(Assembly assembly) => assembly.GetName().Name!;

    protected static void ShouldPass(TestResult result) =>
        result.IsSuccessful.ShouldBeTrue(
            $"Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
}
