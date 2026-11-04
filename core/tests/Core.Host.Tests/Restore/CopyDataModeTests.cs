using System.Globalization;
using Core.Host.Restore;
using Core.Persistence.Custody.Verification;
using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Tests.Restore;

/// <summary>
/// The copy-data mode (issue 66, CA-1) is asked for by the first argument alone, refuses to run without the folder before reading any configuration, and refuses a folder that holds something
/// before touching the database, so a copy is never mixed with what was there.
/// </summary>
public sealed class CopyDataModeTests
{
    private const string Unreachable = "Host=127.0.0.1;Port=1;Username=u;Password=ZQX-never-printed-9921;Database=d;Timeout=2;Pooling=false";

    [Theory]
    [InlineData("copy-data --to pasta", true)]
    [InlineData("copy-data", true)]
    [InlineData("rehearse-restore --seal x", false)]
    [InlineData("--to pasta copy-data", false)]
    [InlineData("", false)]
    public void ModeIsAskedForByTheFirstArgumentAlone(string line, bool requested)
    {
        ArgumentNullException.ThrowIfNull(line);

        Assert.Equal(requested, CopyDataMode.Requested(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Theory]
    [InlineData("copy-data")]
    [InlineData("copy-data --to")]
    [InlineData("copy-data --to a --to b")]
    public Task WithoutTheFolderGivenOnceTheModeRefusesAndWritesNothing(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return RefusalWithoutFolderAsync(line);
    }

    private static async Task RefusalWithoutFolderAsync(string line)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var launch = await CopyDataMode.RunAsync(line.Split(' '), output, error);

        Assert.Equal((ExitCode.ConfigurationRefused, string.Empty), (await launch.RunAsync(), output.ToString()));
        Assert.Contains("informe a pasta da cópia uma vez", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DatabaseThatDoesNotAnswerEndsInCopyFailedAndNeverPrintsTheCredential()
    {
        var folder = Path.Combine(Path.GetTempPath(), "zqx-copy-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(Unreachable);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exit = await CopyDataMode.CopyAsync(dataSource, new CopyDataMode.Request(folder, []), new CopyDataMode.Words(output, error));

            Assert.Equal((ExitCode.CopyFailed, string.Empty), (exit, output.ToString()));
            Assert.Contains("falhou ao falar com o banco", error.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("ZQX-never-printed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(DataCopyOutcome.FolderNotEmpty, "a pasta já tem conteúdo")]
    [InlineData(DataCopyOutcome.SealNotRecorded, "a cadeia não aceitou o evento do selo")]
    [InlineData(DataCopyOutcome.ChainMoved, "outra gravação a alcançou antes da leitura")]
    public async Task EachRefusalOfTheCopySaysItsReasonAndPrintsNothingToNote(DataCopyOutcome outcome, string reason)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await CopyDataMode.ReportAsync(new DataCopyResult(outcome, Seal: null), new CopyDataMode.Words(output, error));

        Assert.Equal((ExitCode.CopyRefused, string.Empty), (exit, output.ToString()));
        Assert.Contains(reason, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CopyThatWasMadePrintsTheTopOfItWithTheLineTheLoadTakes()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var top = ChainSeal.Of(DateTimeOffset.UnixEpoch, 3, new byte[32]);

        var exit = await CopyDataMode.ReportAsync(new DataCopyResult(DataCopyOutcome.Made, top), new CopyDataMode.Words(output, error));

        Assert.Equal((ExitCode.Success, string.Empty), (exit, error.ToString()));
        Assert.Contains($"restore-load <pasta> 3 {top.Digest}", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FolderThatHoldsSomethingRefusesBeforeTouchingTheDatabaseAndNeverPrintsTheCredential()
    {
        var folder = Path.Combine(Path.GetTempPath(), "zqx-copy-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        _ = Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "other.txt"), "zqx", TestContext.Current.CancellationToken);
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(Unreachable);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exit = await CopyDataMode.CopyAsync(dataSource, new CopyDataMode.Request(folder, []), new CopyDataMode.Words(output, error));

            Assert.Equal((ExitCode.CopyRefused, string.Empty), (exit, output.ToString()));
            Assert.Contains("a pasta já tem conteúdo", error.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("ZQX-never-printed", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
