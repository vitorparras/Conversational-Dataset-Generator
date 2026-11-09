using Core.Host.Settings;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;
using Core.Platform.Startup;

namespace Core.Host.Tests.Settings;

/// <summary>
/// The configuration and the mode of the applier (#634, part 4; decision 0053, CA-4): the file of its own declares the file of the core, the restart command and the database that only reads;
/// a key missing, a path that is relative or does not exist, or a database that cannot be reached refuses the start and names the key, and nothing is written.
/// </summary>
public sealed class ApplierConfigurationTests : IDisposable
{
    private readonly TemporaryDirectory folder = new();

    [Fact]
    public void AMissingKeyIsRefusedNamingIt()
    {
        var loaded = ClosedConfiguration.Load(ApplierConfiguration.Schema, new StrictArgumentsSource(DatabaseArguments()));

        Assert.True(loaded.Refused);
        Assert.Contains(loaded.Problems, problem => problem.Key == ApplierConfiguration.SettingsFilePath);
        Assert.Contains(loaded.Problems, problem => problem.Key == ApplierConfiguration.RestartCommandPath);
    }

    [Theory]
    [InlineData("relativo/reiniciar.sh")]
    [InlineData("zqx-nao-existe-6340")]
    public void ARestartCommandThatIsRelativeOrDoesNotExistIsRefusedNamingTheKey(string command)
    {
        var given = command.StartsWith("zqx", StringComparison.Ordinal) ? Path.Combine(folder.Path, command) : command;
        var values = Values(settings: Existing(), command: given);

        var refusal = Assert.Single(ApplierConfiguration.Refusals(values));

        Assert.Equal((ApplierConfiguration.RestartCommandPath, values[ApplierConfiguration.RestartCommandPath]), (refusal.Key, refusal.Found));
    }

    [Fact]
    public void AbsolutePathsThatExistAreAccepted() =>
        Assert.Empty(ApplierConfiguration.Refusals(Values(settings: Existing(), command: Existing())));

    [Fact]
    public void TheArgumentsOfTheRestartAreSplitAtTheSeparatorAndAreNoneWhenAbsent()
    {
        var values = Values(settings: Existing(), command: Existing());
        values[ApplierConfiguration.RestartArgumentsPath] = "restart|nucleo.service";

        Assert.Equal(new[] { "restart", "nucleo.service" }, ApplierConfiguration.RestartArguments(values));
        Assert.Empty(ApplierConfiguration.RestartArguments(Values(settings: Existing(), command: Existing())));
    }

    [Fact]
    public void TheModeIsAskedForByItsWordAsTheFirstArgumentOnly()
    {
        Assert.True(ApplyConfigurationMode.Requested(new[] { "apply-configuration" }));
        Assert.False(ApplyConfigurationMode.Requested(new[] { "--Database:Host=zqx", "apply-configuration" }));
    }

    [Fact]
    public async Task ARelativeRestartCommandEndsTheModeWithTheConfigurationCodeAndNothingIsWritten()
    {
        var file = Applier(settings: Existing(), command: "relativo/reiniciar.sh");

        var launch = await ApplyConfigurationMode.RunAsync(DatabaseArguments(), file, TextWriter.Null, TextWriter.Null);

        Assert.Equal(ExitCode.ConfigurationRefused, await launch.RunAsync());
    }

    [Fact]
    public async Task ADatabaseThatCannotBeReachedEndsTheModeWithItsOwnCode()
    {
        var file = Applier(settings: Existing(), command: Existing());

        var launch = await ApplyConfigurationMode.RunAsync(DatabaseArguments(), file, TextWriter.Null, new StringWriter());

        Assert.Equal(ExitCode.ApplierFailed, await launch.RunAsync());
    }

    [Fact]
    public async Task ARestartCommandThatDoesNotStartIsARestartThatDidNotHappen() =>
        Assert.False(await new ServiceRestart(Path.Combine(folder.Path, "zqx-nao-existe-6340"), new List<string>()).RunAsync(TestContext.Current.CancellationToken));

    public void Dispose() => folder.Dispose();

    private static string[] DatabaseArguments() =>
        new[] { "--Database:Host=127.0.0.1:1", "--Database:Name=zqx", "--Database:Username=zqx_leitura", "--Database:Password=ZQX-SENHA-6340" };

    private static Dictionary<string, string> Values(string settings, string command) => new(StringComparer.Ordinal)
    {
        [ApplierConfiguration.SettingsFilePath] = settings,
        [ApplierConfiguration.RestartCommandPath] = command,
    };

    private string Existing()
    {
        var path = Path.Combine(folder.Path, $"{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "{}");
        return path;
    }

    private string Applier(string settings, string command)
    {
        var path = Path.Combine(folder.Path, "applier.json");
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new { Applier = new { SettingsFile = settings, RestartCommand = command } }));
        return path;
    }
}
