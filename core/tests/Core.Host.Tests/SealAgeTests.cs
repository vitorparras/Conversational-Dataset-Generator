namespace Core.Host.Tests;

/// <summary>
/// The warning of the manual situation (issue #62, part B2b): none when no seal was ever recorded is a warning,
/// a seal of 8 days is a warning, one of 6 days is not, and the other situations never warn.
/// </summary>
public sealed class SealAgeTests
{
    private static readonly DateTimeOffset Now = new(2031, 3, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ManualWithoutAnySealWarnsAndNamesWhoDoesItAndNoRoute()
    {
        var warning = SealAge.Warning(SealSituation.Manual, null, Now);

        Assert.Contains("administração técnica", warning, StringComparison.Ordinal);
        Assert.DoesNotContain("/custody", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualWithTheLastSealEightDaysOldWarnsAndNamesWhoDoesIt()
    {
        var warning = SealAge.Warning(SealSituation.Manual, Now.AddDays(-8), Now);

        Assert.Contains("mais de 7 dias", warning, StringComparison.Ordinal);
        Assert.Contains("administração técnica", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualWithTheLastSealSixDaysOldDoesNotWarn() =>
        Assert.Null(SealAge.Warning(SealSituation.Manual, Now.AddDays(-6), Now));

    [Fact]
    public void ManualWithTheLastSealExactlySevenDaysOldDoesNotWarnAndOneSecondMoreDoes()
    {
        Assert.Null(SealAge.Warning(SealSituation.Manual, Now.AddDays(-7), Now));
        Assert.NotNull(SealAge.Warning(SealSituation.Manual, Now.AddDays(-7).AddSeconds(-1), Now));
    }

    [Theory]
    [InlineData("External")]
    [InlineData("Internal")]
    public void TheSituationsThatSendTheSealNeverWarnWhateverTheAge(string situation)
    {
        var parsed = Enum.Parse<SealSituation>(situation);

        Assert.Null(SealAge.Warning(parsed, null, Now));
        Assert.Null(SealAge.Warning(parsed, Now.AddDays(-30), Now));
    }
}
