using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Auth.ArchitectureTests;

/// <summary>
/// AD-3: the hexagonal layering rules from AD-2, enforced by tests rather than only by
/// convention. The layers are still empty at this point in the setup slice, so most rules
/// pass vacuously - they exist so the first real class added in each layer is checked
/// immediately.
/// </summary>
public class LayeringTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            System.Reflection.Assembly.Load("Auth.Domain"),
            System.Reflection.Assembly.Load("Auth.Application"),
            System.Reflection.Assembly.Load("Auth.Infrastructure"),
            System.Reflection.Assembly.Load("Auth.Api"))
        .Build();

    private static readonly IObjectProvider<IType> DomainTypes =
        Types().That().ResideInAssembly("Auth.Domain").As("Domain types");

    private static readonly IObjectProvider<IType> ApplicationTypes =
        Types().That().ResideInAssembly("Auth.Application").As("Application types");

    private static readonly IObjectProvider<IType> InfrastructureTypes =
        Types().That().ResideInAssembly("Auth.Infrastructure").As("Infrastructure types");

    private static readonly IObjectProvider<IType> ApiTypes =
        Types().That().ResideInAssembly("Auth.Api").As("Api types");

    // ResideInNamespace in this ArchUnitNET version matches on exact namespace only (no regex
    // overload), so nested namespaces like Microsoft.EntityFrameworkCore.Design are matched via
    // a full-name substring check instead.
    private static readonly IObjectProvider<IType> EfCoreTypes =
        Types().That().HaveFullNameContaining("Microsoft.EntityFrameworkCore")
            .As("EF Core types");

    private static readonly IObjectProvider<IType> AspNetCoreTypes =
        Types().That().HaveFullNameContaining("Microsoft.AspNetCore")
            .As("ASP.NET Core types");

    // NotDependOnAny takes a single IObjectProvider, so combining several layers/namespaces
    // into one forbidden set is built with .Or() at the predicate ("That()") level, not by
    // combining already-built IObjectProvider instances (which has no .Or() extension).
    private static readonly IObjectProvider<IType> ApplicationOrInfrastructureOrApiTypes =
        Types().That().ResideInAssembly("Auth.Application")
            .Or().ResideInAssembly("Auth.Infrastructure")
            .Or().ResideInAssembly("Auth.Api")
            .As("Application, Infrastructure or Api types");

    private static readonly IObjectProvider<IType> EfCoreOrInfrastructureTypes =
        Types().That().HaveFullNameContaining("Microsoft.EntityFrameworkCore")
            .Or().ResideInAssembly("Auth.Infrastructure")
            .As("EF Core or Infrastructure types");

    // ArchUnitNET fails a rule by default if its "given" set matches zero types ("requires
    // positive evaluation"), to catch predicate typos. Every layer is still empty at this
    // point in the setup slice, so every rule here is vacuous today - .WithoutRequiringPositiveResults()
    // opts out of that check; the rule still fires for real once CAP-1 adds actual classes.

    [Fact]
    public void Domain_does_not_depend_on_any_other_layer()
    {
        Types().That().Are(DomainTypes)
            .Should().NotDependOnAny(ApplicationOrInfrastructureOrApiTypes)
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure()
    {
        Types().That().Are(ApplicationTypes)
            .Should().NotDependOnAny(InfrastructureTypes)
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Application_does_not_depend_on_ef_core()
    {
        Types().That().Are(ApplicationTypes)
            .Should().NotDependOnAny(EfCoreTypes)
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Application_does_not_depend_on_asp_net_core()
    {
        Types().That().Are(ApplicationTypes)
            .Should().NotDependOnAny(AspNetCoreTypes)
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }

    [Fact]
    public void Api_does_not_use_ef_core_db_context_outside_composition_root()
    {
        // Only Program (the composition root) may touch EF Core / Infrastructure types
        // directly; every other Api type must not.
        Types().That().Are(ApiTypes).And().DoNotHaveName("Program")
            .Should().NotDependOnAny(EfCoreOrInfrastructureTypes)
            .WithoutRequiringPositiveResults()
            .Check(Architecture);
    }
}
