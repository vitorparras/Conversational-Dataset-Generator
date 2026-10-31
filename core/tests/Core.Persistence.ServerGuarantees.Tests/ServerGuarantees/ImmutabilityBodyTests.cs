using System.Text.RegularExpressions;
using Core.Persistence.Migrations;
using Core.Persistence.ServerGuarantees;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>The body the check expects of each function is the one the embedded migration 0009 writes, so that the check holds no copy that could drift from the migration unnoticed.</summary>
public sealed partial class ImmutabilityBodyTests
{
    [Theory]
    [InlineData("refuse_custody_event_change", ImmutabilityCheck.ChangedBody)]
    [InlineData("refuse_custody_event_emptying", ImmutabilityCheck.EmptiedBody)]
    public void BodyTheCheckExpectsIsTheOneTheEmbeddedMigrationWrites(string function, string expected)
    {
        var declared = Declaration(function).Match(MigrationSql());

        Assert.True(declared.Success, $"The migration 0009 does not declare public.{function} with a body between $refuse$ quotes.");
        Assert.Equal(expected, declared.Groups["body"].Value);
        Assert.Equal(ImmutabilityCheck.ExpectedLanguage, declared.Groups["language"].Value);
    }

    [Fact]
    public void MigrationQuotesTheBodyOfNoFunctionBesidesTheTwoTheCheckKnows() =>
        Assert.Equal(2, Quoted().Count(MigrationSql()));

    private static string MigrationSql() => Assert.Single(EmbeddedMigrations.Load(), candidate => candidate.Version == 9).Sql;

    private static Regex Declaration(string function) => new(
        $@"CREATE FUNCTION public\.{function}\(\) RETURNS trigger\s+LANGUAGE (?<language>\w+)\s+AS \$refuse\$(?<body>.*?)\$refuse\$",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    [GeneratedRegex(@"\$refuse\$.*?\$refuse\$", RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 5000)]
    private static partial Regex Quoted();
}
