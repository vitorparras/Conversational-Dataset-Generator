namespace Core.Host.Settings;

/// <summary>A decision on a change of configuration as the screen sends it: the submission, whether it is approved, and who decides.</summary>
internal sealed record DecisionRequest(long ChangeId, bool Approved, long DeciderId);
