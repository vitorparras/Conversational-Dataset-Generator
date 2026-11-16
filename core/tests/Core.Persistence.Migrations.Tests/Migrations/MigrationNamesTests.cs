using System.Runtime.CompilerServices;
using Core.Persistence.Migrations;

namespace Core.Persistence.Tests.Migrations;

/// <summary>A step is named by its order: four digits for the numbered ones, twelve (year to minute) for the ones dated by the author.</summary>
public sealed class MigrationNamesTests
{
    [Fact]
    public void NumberedStepsComeFirstAndDatedStepsFollowByTheirMoment()
    {
        var ordered = MigrationNames.Ordered(["202611171000-b", "0002-b", "202611161500-a", "0001-a", "0010-c"]);

        Assert.Equal(["0001-a", "0002-b", "0010-c", "202611161500-a", "202611171000-b"], ordered);
    }

    [Theory]
    [InlineData("55-short")]
    [InlineData("20261116-eight-digits")]
    [InlineData("0055_underscore")]
    [InlineData("0055")]
    [InlineData("202611161500")]
    [InlineData("name-only")]
    public void ANameOutsideBothFormatsIsADefectOfTheBuild(string name)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => MigrationNames.Ordered([name]));

        Assert.Contains(name, failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("202613161500-month-thirteen")]
    [InlineData("202611321500-day-thirty-two")]
    [InlineData("202611162500-hour-twenty-five")]
    public void ADatedNameThatIsNoMomentOfTheCalendarIsADefectOfTheBuild(string name)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => MigrationNames.Ordered([name]));

        Assert.Contains(name, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheStepsTheBuildCarriesAreInStrictlyIncreasingOrderOfName()
    {
        var names = EmbeddedMigrations.Load().Select(step => step.Name).ToList();

        Assert.Equal(names.Order(StringComparer.Ordinal).Distinct(), names);
    }

    [Fact]
    public void TheNumberedStepsRunFromOneWithoutGapsAndNoDatedStepPrecedesThem()
    {
        var names = EmbeddedMigrations.Load().Select(step => step.Name).ToList();
        var numbered = names.TakeWhile(name => name.IndexOf('-', StringComparison.Ordinal) == 4).ToList();

        Assert.Equal(Enumerable.Range(1, numbered.Count).Select(number => $"{number:0000}-"), numbered.Select(name => name[..5]));
        Assert.All(names.Skip(numbered.Count), name => Assert.Equal(12, name.IndexOf('-', StringComparison.Ordinal)));
    }

    [Fact]
    public void EverySqlFileOfTheFolderIsAStepTheBuildCarries()
    {
        var onDisk = Directory.EnumerateFiles(SqlFolder(), "*.sql").Select(path => Path.GetFileNameWithoutExtension(path)).Order(StringComparer.Ordinal);

        Assert.Equal(onDisk, EmbeddedMigrations.Load().Select(step => step.Name));
    }

    private static string SqlFolder([CallerFilePath] string testFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", "..", "..", "src", "Core.Persistence", "Migrations", "Sql"));
}
