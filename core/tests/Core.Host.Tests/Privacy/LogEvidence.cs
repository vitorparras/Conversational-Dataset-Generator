namespace Core.Host.Tests.Privacy;

/// <summary>
/// Everything a run left to be read: the two streams, the file named by <c>Host:LogFile</c> when there is one, and the
/// time the run began, from which the files shared with the machine are read; without it the run wrote no file besides the log file.
/// </summary>
internal sealed record LogEvidence(string Marker, string Stdout, string Stderr, string? LogFile, DateTimeOffset? Since);
