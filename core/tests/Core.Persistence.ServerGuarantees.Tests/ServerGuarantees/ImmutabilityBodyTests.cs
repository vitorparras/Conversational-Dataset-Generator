using System.Text.RegularExpressions;
using Core.Persistence.Migrations;
using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// The body the check expects of each function is the one the latest embedded migration that declares it writes (the 0009 declared them and the 0014 replaced them), so that the check holds no copy
/// that could drift from the migrations unnoticed.
/// </summary>
public sealed partial class ImmutabilityBodyTests
{
    [Theory]
    [InlineData("refuse_custody_event_change", ImmutabilityCheck.ChangedBody)]
    [InlineData("refuse_custody_event_emptying", ImmutabilityCheck.EmptiedBody)]
    public void BodyTheCheckExpectsIsTheOneTheLatestEmbeddedMigrationWrites(string function, string expected)
    {
        var declared = Declaration(function).Match(LatestDeclaring(function).Sql);

        Assert.True(declared.Success, $"No embedded migration declares public.{function} with a body between $refuse$ quotes.");
        Assert.Equal(expected, declared.Groups["body"].Value);
        Assert.Equal(ImmutabilityCheck.ExpectedLanguage, declared.Groups["language"].Value);
    }

    [Theory]
    [InlineData("refuse_custody_event_change")]
    [InlineData("refuse_custody_event_emptying")]
    public void LatestMigrationThatDeclaresTheFunctionIsTheOneThatLogsTheRefusal(string function) =>
        Assert.Equal(14, LatestDeclaring(function).Version);

    [Theory]
    [InlineData("refuse_custody_event_change", true)]
    [InlineData("refuse_custody_event_emptying", false)]
    public void FunctionAsTheMigration0009WroteItWithoutTheLineOfTheLogIsAccused(string function, bool changed)
    {
        var body = Declaration(function).Match(Assert.Single(EmbeddedMigrations.Load(), candidate => candidate.Version == 9).Sql).Groups["body"].Value;
        var old = ImmutabilityCheck.TriggerReading.Declared("A", body);
        var current = changed
            ? ImmutabilityCheck.Judge(old, ImmutabilityCheck.TriggerReading.Declared("A", ImmutabilityCheck.EmptiedBody))
            : ImmutabilityCheck.Judge(ImmutabilityCheck.TriggerReading.Declared("A", ImmutabilityCheck.ChangedBody), old);

        Assert.DoesNotContain("RAISE LOG", body, StringComparison.Ordinal);
        Assert.Equal(CheckStatus.Failed, current.Status);
        Assert.Contains($"a função public.{function}", current.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LatestMigrationQuotesTheBodyOfNoFunctionBesidesTheTwoTheCheckKnows() =>
        Assert.Equal(2, Quoted().Count(LatestDeclaring("refuse_custody_event_change").Sql));

    private static MigrationStep LatestDeclaring(string function) =>
        EmbeddedMigrations.Load().Where(candidate => Declaration(function).IsMatch(candidate.Sql)).MaxBy(candidate => candidate.Version)
        ?? throw new InvalidOperationException($"No embedded migration declares public.{function}.");

    private static Regex Declaration(string function) => new(
        $@"CREATE (?:OR REPLACE )?FUNCTION public\.{function}\(\) RETURNS trigger\s+LANGUAGE (?<language>\w+)\s+AS \$refuse\$(?<body>.*?)\$refuse\$",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [GeneratedRegex(@"\$refuse\$.*?\$refuse\$", RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex Quoted();
}
