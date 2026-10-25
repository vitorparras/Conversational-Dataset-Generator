namespace Core.Tests.Architecture.CodeRules;

/// <summary>A build the build files cannot tell from the gate's: where the canary routes sit, what the build said, and what the compiler was given.</summary>
internal sealed record UnseenBuild(string RepositoryRoot, IReadOnlyList<CanarySite> Sites, CommandOutcome Build, CompilerInvocation Invocation);
