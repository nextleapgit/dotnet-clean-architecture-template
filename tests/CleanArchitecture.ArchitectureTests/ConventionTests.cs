using System.Reflection;
using CleanArchitecture.SharedKernel;

namespace CleanArchitecture.ArchitectureTests;

public sealed class ConventionTests : BaseTest
{
    [Theory]
    [InlineData("CommandHandler")]
    [InlineData("QueryHandler")]
    [InlineData("DomainEventHandler")]
    [InlineData("Validator")]
    public void ApplicationTypes_Should_BeInternalAndSealed(string nameSuffix) =>
        ShouldPass(Types.InAssembly(ApplicationAssembly)
            .That().HaveNameEndingWith(nameSuffix).And().AreClasses()
            .Should().BeSealed().And().NotBePublic()
            .GetResult());

    [Fact]
    public void InfrastructureStores_Should_BeInternalAndSealed() =>
        ShouldPass(Types.InAssembly(InfrastructureAssembly)
            .That().HaveNameEndingWith("Store").And().AreClasses()
            .Should().BeSealed().And().NotBePublic()
            .GetResult());

    [Fact]
    public void StoreInterfaces_Should_LiveInApplication() =>
        ShouldPass(Types.InAssembly(InfrastructureAssembly)
            .That().AreInterfaces()
            .Should().NotHaveNameEndingWith("Store")
            .GetResult());

    [Fact]
    public void DomainEntities_Should_NotExposePublicSetters()
    {
        string[] violations = DomainAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.Namespace?.StartsWith("CleanArchitecture.Domain", StringComparison.Ordinal) == true)
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(property => property.SetMethod?.IsPublic == true && !IsInitOnly(property.SetMethod))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .ToArray();

        violations.ShouldBeEmpty();
    }

    // init accessors (e.g. positional records) are immutable after construction.
    private static bool IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));

    [Fact]
    public void DomainEvents_Should_BeSealedRecords() =>
        ShouldPass(Types.InAssembly(DomainAssembly)
            .That().ImplementInterface(typeof(IDomainEvent))
            .Should().BeSealed().And().HaveNameEndingWith("DomainEvent")
            .GetResult());

    [Fact]
    public void Endpoints_Should_BeInternalAndSealed() =>
        ShouldPass(Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace("CleanArchitecture.Api.Endpoints").And().AreClasses()
            .And().DoNotHaveName("Tags")
            .And().AreNotNested()
            .Should().BeSealed().And().NotBePublic()
            .GetResult());
}
