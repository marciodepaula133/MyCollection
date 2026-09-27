using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace Auth.ArchitectureTests;

/// <summary>
/// AD-3 also requires checking each layer's .csproj directly: a PackageReference alone
/// (e.g. EF Core added to Application "just for a DTO") can break AD-2 without ever
/// showing up as an IL-level type dependency that ArchUnitNET (see <see cref="LayeringTests"/>)
/// would catch.
/// </summary>
public class ProjectReferenceTests
{
    private static readonly string[] ForbiddenInApplication =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql.EntityFrameworkCore.PostgreSQL",
        "Microsoft.AspNetCore",
    ];

    private static readonly string[] ForbiddenInDomain =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql.EntityFrameworkCore.PostgreSQL",
        "Microsoft.AspNetCore",
    ];

    [Fact]
    public void Domain_csproj_has_no_framework_package_references()
    {
        AssertNoForbiddenPackageReference("Auth.Domain", ForbiddenInDomain);
    }

    [Fact]
    public void Application_csproj_has_no_ef_core_or_asp_net_core_package_references()
    {
        AssertNoForbiddenPackageReference("Auth.Application", ForbiddenInApplication);
    }

    private static void AssertNoForbiddenPackageReference(string projectName, string[] forbiddenPrefixes)
    {
        var csprojPath = Path.Combine(GetRepoRoot(), "apps", "auth-api", "src", projectName, $"{projectName}.csproj");
        Assert.True(File.Exists(csprojPath), $"Expected to find {csprojPath}");

        var document = XDocument.Load(csprojPath);
        var packageReferences = document
            .Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .ToList();

        foreach (var forbiddenPrefix in forbiddenPrefixes)
        {
            Assert.DoesNotContain(
                packageReferences,
                reference => reference.StartsWith(forbiddenPrefix, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string GetRepoRoot([CallerFilePath] string sourceFile = "")
    {
        // This file lives at apps/auth-api/tests/Auth.ArchitectureTests/ProjectReferenceTests.cs;
        // four levels up is the repo root.
        var directory = Path.GetDirectoryName(sourceFile)!;
        return Path.GetFullPath(Path.Combine(directory, "..", "..", "..", ".."));
    }
}
