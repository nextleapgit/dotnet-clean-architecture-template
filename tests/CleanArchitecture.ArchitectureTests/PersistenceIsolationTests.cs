using System.Reflection;

namespace CleanArchitecture.ArchitectureTests;

/// <summary>EF Core is allowed only in Infrastructure (and tests).</summary>
public sealed class PersistenceIsolationTests : BaseTest
{
    public static TheoryData<string> PersistenceAgnosticAssemblies =>
    [
        Name(SharedKernelAssembly),
        Name(BuildingBlocksAssembly),
        Name(DomainAssembly),
        Name(ApplicationAssembly)
    ];

    [Theory]
    [MemberData(nameof(PersistenceAgnosticAssemblies))]
    public void Assembly_Should_NotReferenceEntityFrameworkCore(string assemblyName)
    {
        Assembly assembly = AllAssemblies.Single(a => Name(a) == assemblyName);

        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ShouldNotContain(name => name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));

        ShouldPass(Types.InAssembly(assembly).Should().NotHaveDependencyOn("Microsoft.EntityFrameworkCore").GetResult());
    }
}
