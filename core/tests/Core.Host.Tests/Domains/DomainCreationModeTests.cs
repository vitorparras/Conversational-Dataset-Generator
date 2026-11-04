using Core.Host.Domains;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Tests.Domains;

/// <summary>
/// The create-domain mode (issue 54, CA-4) is asked for by the first argument alone, takes the name once, refuses without touching the database when it cannot go on,
/// and never prints the credential.
/// </summary>
public sealed class DomainCreationModeTests
{
    private const string Unreachable = "Host=127.0.0.1;Port=1;Username=u;Password=ZQX-never-printed-7731;Database=d;Timeout=2;Pooling=false";

    [Theory]
    [InlineData("create-domain --name x", true)]
    [InlineData("create-domain", true)]
    [InlineData("rotate-epoch", false)]
    [InlineData("--name x create-domain", false)]
    [InlineData("", false)]
    public void ModeIsAskedForByTheFirstArgumentAlone(string line, bool requested)
    {
        ArgumentNullException.ThrowIfNull(line);

        Assert.Equal(requested, DomainCreationMode.Requested(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Theory]
    [InlineData("create-domain --name zqx-a --Database:Host=h", "zqx-a", "--Database:Host=h")]
    [InlineData("create-domain --Database:Host=h --name zqx-a", "zqx-a", "--Database:Host=h")]
    [InlineData("create-domain --name=zqx-a", "zqx-a", "")]
    [InlineData("create-domain --name zqx-a --name zqx-b", null, "")]
    [InlineData("create-domain --name=zqx-a --name zqx-b", null, "")]
    [InlineData("create-domain --name", null, "")]
    [InlineData("create-domain --name=", null, "")]
    [InlineData("create-domain", null, "")]
    public void TheNameIsTakenOnceAndTheRestGoesToTheConfiguration(string line, string? name, string rest)
    {
        ArgumentNullException.ThrowIfNull(line);

        var (found, remaining) = DomainCreationMode.Split(line.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        Assert.Equal((name, rest), (found, string.Join(' ', remaining)));
    }

    [Fact]
    public void ANameWithSpacesIsTakenWholeWhenTheShellKeepsItInOneArgument() =>
        Assert.Equal("domínio de teste", DomainCreationMode.Split(["create-domain", "--name", "domínio de teste"]).Name);

    [Fact]
    public async Task CustodyNotReadRefusesNamingWhyAndTouchesNothing()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var reading = MasterKeyReading.Absent("o arquivo da chave não foi encontrado");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await DomainCreationMode.CreateAsync(dataSource, reading, "zqx-domain", new DomainCreationMode.Words(output, error));

        Assert.Equal((ExitCode.DomainCreationRefused, string.Empty), (exit, output.ToString()));
        Assert.Contains("o arquivo da chave não foi encontrado", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" zqx-domain")]
    [InlineData("zqx-domain ")]
    public async Task ANameEmptyOrPaddedRefusesBeforeTouchingTheDatabase(string name)
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var reading = MasterKeyReading.FromBytes(new byte[MasterKey.Length], DerivationLabels.None, "teste");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await DomainCreationMode.CreateAsync(dataSource, reading, name, new DomainCreationMode.Words(output, error));

        Assert.Equal((ExitCode.DomainCreationRefused, string.Empty), (exit, output.ToString()));
        Assert.Contains("vazio nem ter espaço", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DatabaseThatCannotBeReachedFailsWithItsOwnCodeAndNeverPrintsTheCredentialNorTheName()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var reading = MasterKeyReading.FromBytes(new byte[MasterKey.Length], DerivationLabels.None, "teste");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await DomainCreationMode.CreateAsync(dataSource, reading, "zqx-name-never-printed", new DomainCreationMode.Words(output, error));

        Assert.Equal(ExitCode.DomainCreationFailed, exit);
        Assert.DoesNotContain("ZQX-never-printed-7731", error.ToString() + output, StringComparison.Ordinal);
        Assert.DoesNotContain("zqx-name-never-printed", error.ToString() + output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExitCodesOfTheCreationAreDistinctFromEachOtherAndFromTheRotation() =>
        Assert.Equal(
            5,
            new[] { ExitCode.DomainLabelsMissing, ExitCode.DomainNameTaken, ExitCode.DomainCreationRefused, ExitCode.DomainCreationFailed, ExitCode.EpochRotationRefused }
                .Distinct().Count());
}
