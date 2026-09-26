namespace CleanArchitecture.ArchitectureTests;

public sealed class ForbiddenDependencyTests : BaseTest
{
    [Fact]
    public void NoAssembly_Should_ReferenceMediatR() =>
        AllAssemblies
            .SelectMany(assembly => assembly.GetReferencedAssemblies())
            .Select(reference => reference.Name)
            .ShouldNotContain(name => name!.StartsWith("MediatR", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Api_Should_NotUseSwaggerUi()
    {
        ApiAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ShouldNotContain(name => name!.StartsWith("Swashbuckle", StringComparison.OrdinalIgnoreCase));

        ApiAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ShouldContain("Scalar.AspNetCore");
    }
}
