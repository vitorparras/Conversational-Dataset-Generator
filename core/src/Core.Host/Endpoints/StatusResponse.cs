using Core.Platform.Startup;

namespace Core.Host.Endpoints;

/// <summary>What the process says about itself: alive is always true when it answers, ready only when no check failed, and when the checks ended.</summary>
internal sealed record StatusResponse(bool Alive, bool Ready, DateTimeOffset CheckedAt, IReadOnlyList<CheckResult> Checks);
