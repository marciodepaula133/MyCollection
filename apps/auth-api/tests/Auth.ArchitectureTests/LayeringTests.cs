using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Auth.ArchitectureTests;

/// <summary>
/// AD-3: the hexagonal layering rules from AD-2, enforced by tests rather than only by
/// convention. Every rule must evaluate at least one type (ArchUnitNET's
/// default), so a typo in a predicate fails loudly instead of passing vacuously.
/// </summary>
public class LayeringTests
{
    private static readonly System.Reflection.Assembly DomainAssembly = System.Reflection.Assembly.Load("Auth.Domain");
    private static readonly System.Reflection.Assembly ApplicationAssembly = System.Reflection.Assembly.Load("Auth.Application");
    private static readonly System.Reflection.Assembly InfrastructureAssembly = System.Reflection.Assembly.Load("Auth.Infrastructure");
    private static readonly System.Reflection.Assembly ApiAssembly = System.Reflection.Assembly.Load("Auth.Api");

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(DomainAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly)
        .Build();

    private static readonly IObjectProvider<IType> DomainTypes =
        Types().That().ResideInAssembly(DomainAssembly).As("Domain types");

    private static readonly IObjectProvider<IType> ApplicationTypes =
        Types().That().ResideInAssembly(ApplicationAssembly).As("Application types");

    private static readonly IObjectProvider<IType> InfrastructureTypes =
        Types().That().ResideInAssembly(InfrastructureAssembly).As("Infrastructure types");

    private static readonly IObjectProvider<IType> ApiTypes =
        Types().That().ResideInAssembly(ApiAssembly).As("Api types");

    // ResideInNamespace in this ArchUnitNET version matches on exact namespace only (no regex
    // overload), so nested namespaces like Microsoft.EntityFrameworkCore.Design are matched via
    // a full-name substring check instead.
    private static readonly IObjectProvider<IType> EfCoreTypes =
        Types().That().HaveFullNameContaining("Microsoft.EntityFrameworkCore")
            .As("EF Core types");

    private static readonly IObjectProvider<IType> AspNetCoreTypes =
        Types().That().HaveFullNameContaining("Microsoft.AspNetCore")
            .As("ASP.NET Core types");

    // Token signing is an Infrastructure adapter; Application sees only the ITokenIssuer port.
    private static readonly IObjectProvider<IType> IdentityModelTypes =
        Types().That().HaveFullNameContaining("IdentityModel")
            .As("IdentityModel types");

    // NotDependOnAny takes a single IObjectProvider, so combining several layers/namespaces
    // into one forbidden set is built with .Or() at the predicate ("That()") level, not by
    // combining already-built IObjectProvider instances (which has no .Or() extension).
    private static readonly IObjectProvider<IType> ApplicationOrInfrastructureOrApiTypes =
        Types().That().ResideInAssembly(ApplicationAssembly)
            .Or().ResideInAssembly(InfrastructureAssembly)
            .Or().ResideInAssembly(ApiAssembly)
            .As("Application, Infrastructure or Api types");

    private static readonly IObjectProvider<IType> EfCoreOrInfrastructureTypes =
        Types().That().HaveFullNameContaining("Microsoft.EntityFrameworkCore")
            .Or().ResideInAssembly(InfrastructureAssembly)
            .As("EF Core or Infrastructure types");

    [Fact]
    public void Domain_does_not_depend_on_any_other_layer()
    {
        Types().That().Are(DomainTypes)
            .Should().NotDependOnAny(ApplicationOrInfrastructureOrApiTypes)
            .Check(Architecture);
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure()
    {
        Types().That().Are(ApplicationTypes)
            .Should().NotDependOnAny(InfrastructureTypes)
            .Check(Architecture);
    }

    [Fact]
    public void Application_does_not_depend_on_ef_core()
    {
        Types().That().Are(ApplicationTypes)
            .Should().NotDependOnAny(EfCoreTypes)
            .Check(Architecture);
    }

    [Fact]
    public void Application_does_not_depend_on_asp_net_core()
    {
        Types().That().Are(ApplicationTypes)
            .Should().NotDependOnAny(AspNetCoreTypes)
            .Check(Architecture);
    }

    [Fact]
    public void Application_does_not_depend_on_identity_model()
    {
        Types().That().Are(ApplicationTypes)
            .Should().NotDependOnAny(IdentityModelTypes)
            .Check(Architecture);
    }

    [Fact]
    public void Api_does_not_use_ef_core_db_context_outside_composition_root()
    {
        // Only Program (the composition root) may touch EF Core / Infrastructure types
        // directly; every other Api type must not.
        Types().That().Are(ApiTypes).And().DoNotHaveName("Program")
            .Should().NotDependOnAny(EfCoreOrInfrastructureTypes)
            .Check(Architecture);
    }
}
