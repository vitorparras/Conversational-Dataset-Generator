namespace Core.Host.Tests.Privacy;

/// <summary>The seeded text of the privacy tests: improbable, and new for each test so tests running together never contaminate each other.</summary>
internal static class LogMarker
{
    public static string Fresh() => $"ZQX-MARKER-0028-{Guid.NewGuid():N}";
}
