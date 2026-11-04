using Core.Host.Operators;
using Core.Persistence.People;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Tests.Operators;

/// <summary>
/// The create-person mode (issue 65) is asked for by the first argument alone, takes the name and the function once each and never a password, refuses a field it does not accept
/// before touching the database, and never prints the credential, the name or the password.
/// </summary>
public sealed class FirstOperatorModeTests
{
    private const string Unreachable = "Host=127.0.0.1;Port=1;Username=u;Password=ZQX-never-printed-7731;Database=d;Timeout=2;Pooling=false";
    private const string Password = "ZQX-password-never-printed-4408";
    private const string Name = "zqx-name-never-printed";

    [Theory]
    [InlineData("create-person --name x", true)]
    [InlineData("create-person", true)]
    [InlineData("create-domain", false)]
    [InlineData("--name x create-person", false)]
    [InlineData("", false)]
    public void ModeIsAskedForByTheFirstArgumentAlone(string line, bool requested)
    {
        ArgumentNullException.ThrowIfNull(line);

        Assert.Equal(requested, FirstOperatorMode.Requested(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Theory]
    [InlineData("create-person --name zqx-a --function Auditoria --Database:Host=h", "zqx-a", "Auditoria", "--Database:Host=h")]
    [InlineData("create-person --Database:Host=h --function=Auditoria --name=zqx-a", "zqx-a", "Auditoria", "--Database:Host=h")]
    [InlineData("create-person --name zqx-a --name zqx-b --function Auditoria", null, "Auditoria", "")]
    [InlineData("create-person --name zqx-a --function", "zqx-a", "", "")]
    [InlineData("create-person --function=Auditoria", null, "Auditoria", "")]
    [InlineData("create-person", null, null, "")]
    public void TheNameAndTheFunctionAreTakenOnceEachAndTheRestGoesToTheConfiguration(string line, string? name, string? function, string rest)
    {
        ArgumentNullException.ThrowIfNull(line);

        var asked = FirstOperatorArguments.Split(line.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        Assert.Equal((name, function, rest), (asked.Name, asked.Function, string.Join(' ', asked.Remaining)));
    }

    [Theory]
    [InlineData("--password", "x")]
    [InlineData("--password=x", "")]
    public void APasswordAsAnArgumentIsOfferedAndKeptOutOfTheConfiguration(string option, string value)
    {
        var args = new[] { "create-person", "--name", "zqx-a", option, value }.Where(argument => argument.Length > 0).ToArray();

        var asked = FirstOperatorArguments.Split(args);

        Assert.Equal((true, string.Empty), (asked.PasswordOffered, string.Join(' ', asked.Remaining)));
    }

    [Theory]
    [InlineData("--password=" + Password)]
    [InlineData("--password")]
    public async Task APasswordAsAnArgumentRefusesWithTheConfigurationCodeAndRepeatsNothing(string option)
    {
        using var input = new StringReader(Password + "\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var launch = await FirstOperatorMode.RunAsync(["create-person", "--name", Name, "--function", "Auditoria", option, Password], input, output, error);

        Assert.Equal((ExitCode.ConfigurationRefused, string.Empty), (await launch.RunAsync(), output.ToString()));
        Assert.Contains("entrada padrão", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Password, error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zqx-name", "Cozinheiro", "função")]
    [InlineData("zqx-name", "auditoria", "função")]
    [InlineData("", "Auditoria", "nome")]
    [InlineData(" zqx-name", "Auditoria", "nome")]
    [InlineData("zqx-name ", "Auditoria", "nome")]
    public async Task AFieldNotAcceptedRefusesNamingItBeforeTouchingTheDatabase(string name, string function, string field)
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await FirstOperatorMode.CreateAsync(dataSource, new PasswordDerivation(1), new FirstOperator(name, function, Password), new FirstOperatorMode.Words(output, error));

        Assert.Equal((ExitCode.PersonCreationRefused, string.Empty), (exit, output.ToString()));
        Assert.Contains($"o campo '{field}'", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Password, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEmptyPasswordRefusesNamingTheFieldBeforeTouchingTheDatabase()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await FirstOperatorMode.CreateAsync(dataSource, new PasswordDerivation(1), new FirstOperator(Name, "Auditoria", string.Empty), new FirstOperatorMode.Words(output, error));

        Assert.Equal(ExitCode.PersonCreationRefused, exit);
        Assert.Contains("o campo 'senha'", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryFieldAtFaultIsNamedInOneRefusal()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var output = new StringWriter();
        using var error = new StringWriter();

        _ = await FirstOperatorMode.CreateAsync(dataSource, new PasswordDerivation(1), new FirstOperator(string.Empty, "x", string.Empty), new FirstOperatorMode.Words(output, error));

        Assert.All(["'nome'", "'função'", "'senha'"], field => Assert.Contains(field, error.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task DatabaseThatCannotBeReachedFailsWithItsOwnCodeAndNeverPrintsTheCredentialTheNameNorThePassword()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await FirstOperatorMode.CreateAsync(dataSource, new PasswordDerivation(1), new FirstOperator(Name, "Auditoria", Password), new FirstOperatorMode.Words(output, error));

        var words = error.ToString() + output;
        Assert.Equal(ExitCode.PersonCreationFailed, exit);
        Assert.All(["ZQX-never-printed-7731", Name, Password], secret => Assert.DoesNotContain(secret, words, StringComparison.Ordinal));
    }

    [Fact]
    public void ThePasswordIsNotInTheTextFormOfWhatWasAsked() =>
        Assert.DoesNotContain(Password, new FirstOperator(Name, "Auditoria", Password).ToString(), StringComparison.Ordinal);

    [Fact]
    public void TheExitCodesOfTheFirstOperatorAreDistinctFromEachOtherAndFromTheDomain() =>
        Assert.Equal(
            6,
            new[]
            {
                ExitCode.PersonRegistryNotEmpty, ExitCode.PersonCreationRefused, ExitCode.PersonCreationFailed,
                ExitCode.DomainNameTaken, ExitCode.DomainCreationRefused, ExitCode.DomainCreationFailed,
            }.Distinct().Count());
}
