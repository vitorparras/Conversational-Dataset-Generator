using Core.Platform.Startup;

namespace Core.Host.Endpoints;

/// <summary>What the process says about itself: alive is always true when it answers, ready only when every check passed.</summary>
internal sealed record StatusResponse(bool Alive, bool Ready, IReadOnlyList<CheckResult> Checks);
