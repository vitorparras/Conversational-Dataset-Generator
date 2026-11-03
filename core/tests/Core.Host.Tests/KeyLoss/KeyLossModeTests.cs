using Core.Host.KeyLoss;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Tests.KeyLoss;

/// <summary>
/// The declare-key-loss mode (issue 67) is asked for by the first argument alone, refuses a scope or a list of what was tried that is outside the fixed lists before touching the database,
/// fails with a code of its own when the database does not answer, and never prints the credential.
/// </summary>
public sealed class KeyLossModeTests
{
    private const string Unreachable = "Host=127.0.0.1;Port=1;Username=u;Password=ZQX-never-printed-9921;Database=d;Timeout=2;Pooling=false";

    [Theory]
    [InlineData("declare-key-loss --scope master --tried copia-de-guarda", true)]
    [InlineData("declare-key-loss", true)]
    [InlineData("rotate-domain-label --domain 1", false)]
    [InlineData("--scope master declare-key-loss", false)]
    [InlineData("", false)]
    public void ModeIsAskedForByTheFirstArgumentAlone(string line, bool requested)
    {
        ArgumentNullException.ThrowIfNull(line);

        Assert.Equal(requested, KeyLossMode.Requested(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Theory]
    [InlineData("segredo")]
    [InlineData("Master")]
    [InlineData("texto-bruto/0/v1")]
    [InlineData("")]
    public async Task AScopeOutsideTheListRefusesBeforeTouchingTheDatabase(string scope)
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await KeyLossMode.DeclareAsync(dataSource, scope, "copia-de-guarda", new KeyLossMode.Words(output, error));

        Assert.Equal((ExitCode.KeyLossRefused, string.Empty), (exit, output.ToString()));
        Assert.Contains("o escopo é master ou um rótulo", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zqx-livre")]
    [InlineData("copia-de-guarda,copia-de-guarda")]
    [InlineData("copia-de-guarda,")]
    [InlineData("")]
    public async Task WhatWasTriedOutsideTheListRefusesBeforeTouchingTheDatabaseAndTellsTheList(string tried)
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await KeyLossMode.DeclareAsync(dataSource, "master", tried, new KeyLossMode.Words(output, error));

        Assert.Equal((ExitCode.KeyLossRefused, string.Empty), (exit, output.ToString()));
        Assert.Contains("copia-anterior-da-guarda", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DatabaseThatCannotBeReachedFailsWithItsOwnCodeAndNeverPrintsTheCredential()
    {
        await using var dataSource = NpgsqlDataSource.Create(Unreachable);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await KeyLossMode.DeclareAsync(dataSource, "master", "copia-de-guarda", new KeyLossMode.Words(output, error));

        Assert.Equal(ExitCode.KeyLossFailed, exit);
        Assert.DoesNotContain("ZQX-never-printed-9921", error.ToString() + output, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExitCodesOfTheLossAreDistinctFromEachOtherAndFromTheOtherModes() =>
        Assert.Equal(
            9,
            new[]
            {
                ExitCode.KeyLossRefused, ExitCode.KeyLossFailed, ExitCode.DomainLabelRefused, ExitCode.DomainLabelFailed, ExitCode.DomainLabelsMissing,
                ExitCode.DomainNameTaken, ExitCode.DomainCreationRefused, ExitCode.DomainCreationFailed, ExitCode.EpochRotationRefused,
            }.Distinct().Count());
}
