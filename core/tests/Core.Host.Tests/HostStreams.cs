namespace Core.Host.Tests;

/// <summary>What a stopped host and everything it started wrote to the two standard streams.</summary>
internal sealed record HostStreams(string Output, string Error);
