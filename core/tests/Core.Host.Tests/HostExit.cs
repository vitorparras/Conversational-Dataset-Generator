namespace Core.Host.Tests;

/// <summary>How a host run ended: the exit code and everything written to the error and output streams.</summary>
internal sealed record HostExit(int Code, string Error, string Output);
