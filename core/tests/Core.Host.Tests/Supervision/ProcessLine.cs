namespace Core.Host.Tests.Supervision;

/// <summary>A process and the command line the operating system reports for it, which is what any other account on the machine can read.</summary>
internal sealed record ProcessLine(int Id, string CommandLine);
