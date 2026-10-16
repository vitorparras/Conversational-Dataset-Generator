using Core.Platform.Configuration;

namespace Core.Platform.Tests;

/// <summary>A refusal has to say why.</summary>
public sealed class ConfigurationLoadResultTests
{
    [Fact]
    public void RefusalWithoutAProblemIsADefect()
    {
        IReadOnlyList<ConfigurationProblem> noProblems = [];

        var defect = Assert.Throws<ArgumentException>(() => ConfigurationLoadResult.Refusal(noProblems));

        Assert.Contains("at least one problem", defect.Message, StringComparison.Ordinal);
    }
}
