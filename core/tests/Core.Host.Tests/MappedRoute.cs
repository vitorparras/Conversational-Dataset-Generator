using Core.Host.Endpoints;

namespace Core.Host.Tests;

/// <summary>One endpoint the host maps: its method and pattern, whether it answers with no session, whether it is short-circuited past the gate and the access it declares, if it does.</summary>
internal sealed record MappedRoute(string Method, string Pattern, bool Open, bool ShortCircuited, ScreenAccess? Access = null)
{
    public override string ToString() => $"{Method} {Pattern}";
}
