using Core.Platform.Startup;

namespace Core.Platform.Tests.Startup;

/// <summary>A check that concludes what it was told to and records that it ran.</summary>
internal sealed class ScriptedStartupCheck : IStartupCheck
{
    private readonly CheckStatus status;
    private readonly List<string> ran;

    public ScriptedStartupCheck(string name, CheckStatus status, List<string> ran)
    {
        Name = name;
        this.status = status;
        this.ran = ran;
    }

    public string Name { get; }

    public Task<CheckResult> RunAsync(CancellationToken cancellationToken)
    {
        ran.Add(Name);
        return Task.FromResult(new CheckResult(Name, status, "scripted", "scripted source"));
    }
}
