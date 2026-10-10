using Core.Platform.Configuration;
using Core.Platform.Startup;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Tests.Unit.Platform.Startup;

/// <summary>Issue 26: checks run in a fixed order, all of them, each with a named result.</summary>
public sealed class StartupRunnerTests : IDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("cdg-startup-");

    public void Dispose() => directory.Delete(recursive: true);

    [Fact]
    public async Task ChecksRunInTheGivenOrderAndAllRunAfterAFailure()
    {
        var ran = new List<string>();
        IReadOnlyList<IStartupCheck> checks =
        [
            new RecordingCheck("a", CheckStatus.Passed, ran),
            new RecordingCheck("b", CheckStatus.Failed, ran),
            new RecordingCheck("c", CheckStatus.Passed, ran),
        ];

        var report = await StartupRunner.RunAsync(checks, NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Equal(["a", "b", "c"], ran);
        Assert.Equal(["a", "b", "c"], report.Results.Select(r => r.Name));
        Assert.False(report.AllPassed);
    }

    [Fact]
    public async Task CheckThatThrowsIsThatCheckFailingByName()
    {
        IReadOnlyList<IStartupCheck> checks = [new ThrowingCheck()];

        var report = await StartupRunner.RunAsync(checks, NullLogger.Instance, TestContext.Current.CancellationToken);

        var result = Assert.Single(report.Results);
        Assert.Equal(("throwing", CheckStatus.Failed), (result.Name, result.Status));
        Assert.DoesNotContain("ZQX-MARKER-7731", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BundledFilePresentPasses()
    {
        await File.WriteAllTextAsync(Path.Combine(directory.FullName, "model.bin"), "x", TestContext.Current.CancellationToken);
        var check = new BundledFileCheck("model.bin", directory.FullName, ConfigurationOrigin.File);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(("bundled-file:model.bin", CheckStatus.Passed), (result.Name, result.Status));
    }

    [Fact]
    public async Task BundledFileAbsentFailsNamingTheItemAndWhereItShouldComeFrom()
    {
        var check = new BundledFileCheck("model.bin", directory.FullName, ConfigurationOrigin.File);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal("bundled-file:model.bin", result.Name);
        Assert.Contains("'model.bin'", result.Message, StringComparison.Ordinal);
        Assert.Contains("'model.bin' na pasta do executável", result.ExpectedSource, StringComparison.Ordinal);
        Assert.Contains("Installation:BundledFiles (arquivo)", result.ExpectedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(directory.FullName, result.ExpectedSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BundledFileDeclaredWithAnAbsolutePathIsNamedByItsFileNameOnly(bool present)
    {
        var declared = Path.Combine(directory.CreateSubdirectory("elsewhere").FullName, "zqx-model.bin");
        if (present)
        {
            await File.WriteAllTextAsync(declared, "x", TestContext.Current.CancellationToken);
        }

        var check = new BundledFileCheck(declared, AppContext.BaseDirectory, ConfigurationOrigin.File);
        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            ("bundled-file:zqx-model.bin", present ? CheckStatus.Passed : CheckStatus.Failed),
            (result.Name, result.Status));
        Assert.Contains("'zqx-model.bin', no caminho absoluto", result.ExpectedSource, StringComparison.Ordinal);
        Assert.Contains("Installation:BundledFiles (arquivo)", result.ExpectedSource, StringComparison.Ordinal);
        string[] shown = [result.Name, result.Message, result.ExpectedSource];
        Assert.All(shown, text => Assert.DoesNotContain(directory.FullName, text, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EveryDeclaredFileBecomesOneCheckInTheDeclaredOrder()
    {
        var configuration = ClosedConfiguration.Load(
            [ConfigurationSources.Arguments(["--Installation:BundledFiles=b.bin; a.bin"])],
            new ConfigurationSchema([BundledFileCheck.Section])).Configuration!;

        var checks = BundledFileCheck.FromConfiguration(configuration, directory.FullName);

        Assert.Equal(["bundled-file:b.bin", "bundled-file:a.bin"], checks.Select(c => c.Name));
    }

    private sealed class RecordingCheck(string name, CheckStatus status, List<string> ran) : IStartupCheck
    {
        public string Name => name;

        public Task<CheckResult> RunAsync(CancellationToken cancellationToken)
        {
            ran.Add(name);
            return Task.FromResult(new CheckResult(name, status, "detalhe", "origem"));
        }
    }

    private sealed class ThrowingCheck : IStartupCheck
    {
        public string Name => "throwing";

        public Task<CheckResult> RunAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("ZQX-MARKER-7731");
    }
}
