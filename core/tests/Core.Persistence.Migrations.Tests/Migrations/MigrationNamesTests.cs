using Core.Persistence.Migrations;

namespace Core.Persistence.Tests.Migrations;

/// <summary>A step is named by its order: four digits for the numbered ones, twelve (year to minute) for the ones dated by the author.</summary>
public sealed class MigrationNamesTests
{
    private const int LastNumbered = 59;

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
    [InlineData("202613161500-month-thirteen")]
    [InlineData("202611321500-day-thirty-two")]
    [InlineData("202611162500-hour-twenty-five")]
    public void ANameInNeitherFormatOrThatIsNoMomentOfTheCalendarIsADefectOfTheBuild(string name)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => MigrationNames.Ordered([name]));

        Assert.Contains(name, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNumberedStepsRunFromOneToTheLastNumberedWithoutGapsAndNoNewOneJoinsThem()
    {
        var names = EmbeddedMigrations.Load().Select(step => step.Name).ToList();
        var numbered = names.TakeWhile(name => name.IndexOf('-', StringComparison.Ordinal) == MigrationNames.NumberWidth).ToList();

        Assert.Equal(Enumerable.Range(1, LastNumbered).Select(number => $"{number:0000}"), numbered.Select(name => name[..MigrationNames.NumberWidth]));
    }

    [Fact]
    public void EveryStepAfterTheNumberedOnesIsDatedByItsAuthor()
    {
        var names = EmbeddedMigrations.Load().Select(step => step.Name).Skip(LastNumbered).ToList();

        Assert.All(names, name => Assert.Equal(MigrationNames.MomentWidth, name.IndexOf('-', StringComparison.Ordinal)));
    }

    [Fact]
    public void EverySqlFileOfTheFolderIsAStepTheBuildCarries()
    {
        var onDisk = Directory.EnumerateFiles(SqlFolder(), "*.sql").Select(path => Path.GetFileNameWithoutExtension(path)).Order(StringComparer.Ordinal);

        Assert.Equal(onDisk, EmbeddedMigrations.Load().Select(step => step.Name));
    }

    private static string SqlFolder()
    {
        var folder = Path.Combine("core", "src", "Core.Persistence", "Migrations", "Sql");
        for (var up = new DirectoryInfo(AppContext.BaseDirectory); up is not null; up = up.Parent)
        {
            if (Directory.Exists(Path.Combine(up.FullName, folder)))
            {
                return Path.Combine(up.FullName, folder);
            }
        }

        throw new DirectoryNotFoundException($"No parent of the test binaries holds {folder}.");
    }
}
