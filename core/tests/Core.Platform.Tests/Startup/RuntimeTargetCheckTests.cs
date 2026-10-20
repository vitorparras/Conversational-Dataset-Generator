using Core.Platform.Startup;
using Core.Platform.Versions;

namespace Core.Platform.Tests.Startup;

/// <summary>An executable stamped for one target refuses another and names both; one stamped for no target runs anywhere.</summary>
public sealed class RuntimeTargetCheckTests
{
    private const string Machine = "linux-x64";

    [Fact]
    public async Task ForgedTargetFailsNamingTheTargetAndTheMachine()
    {
        var check = new RuntimeTargetCheck(VersionValue.Present("ZQX-RID"), Machine);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal("publicado para o alvo ZQX-RID, mas o destino em que roda é linux-x64", result.Message);
        Assert.Equal("um executável publicado para linux-x64", result.ExpectedSource);
    }

    [Fact]
    public async Task MatchingTargetPasses()
    {
        var check = new RuntimeTargetCheck(VersionValue.Present(Machine), Machine);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.Equal(Machine, result.ExpectedSource);
    }

    [Fact]
    public async Task TargetDifferingOnlyInCaseFails()
    {
        var check = new RuntimeTargetCheck(VersionValue.Present("LINUX-X64"), Machine);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
    }

    [Fact]
    public async Task BuildStampedForNoTargetPasses()
    {
        var check = new RuntimeTargetCheck(VersionValue.Absent, Machine);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.Equal("RuntimeIdentifier", result.Name);
    }

    [Fact]
    public async Task RefusalCarriesNoPathOfTheMachine()
    {
        var check = new RuntimeTargetCheck(VersionValue.Present("ZQX-RID"), Machine);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        var text = result.Message + result.ExpectedSource;
        Assert.DoesNotContain(Path.DirectorySeparatorChar, text);
        Assert.DoesNotContain(Path.AltDirectorySeparatorChar, text);
        Assert.DoesNotContain(AppContext.BaseDirectory, text, StringComparison.Ordinal);
    }
}
