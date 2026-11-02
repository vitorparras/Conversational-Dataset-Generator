namespace Core.Host.Tests;

/// <summary>One endpoint the host maps: its method and pattern, whether it answers with no session and whether it is short-circuited past the gate.</summary>
internal sealed record MappedRoute(string Method, string Pattern, bool Open, bool ShortCircuited)
{
    public override string ToString() => $"{Method} {Pattern}";
}
