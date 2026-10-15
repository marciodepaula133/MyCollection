using System.Reflection;

namespace MyCollection.ServiceDefaults;

/// <summary>
/// AD-16: the build-time OpenAPI generator (<c>GetDocument.Insider</c>) boots and starts the app
/// to read its endpoints. Startup side effects that need real config or secrets (e.g. the JWT
/// signing key check) are skipped when this is true.
/// </summary>
public static class OpenApiGeneration
{
    public static bool IsRunning { get; } =
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
}
