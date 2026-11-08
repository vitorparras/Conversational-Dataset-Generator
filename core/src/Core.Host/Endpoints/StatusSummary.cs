namespace Core.Host.Endpoints;

/// <summary>What <c>/status</c> answers with no session (#632): whether the process is alive and whether it is ready, and nothing of what stands behind the door.</summary>
internal sealed record StatusSummary(bool Alive, bool Ready);
