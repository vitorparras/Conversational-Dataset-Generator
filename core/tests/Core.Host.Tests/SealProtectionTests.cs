namespace Core.Host.Tests;

/// <summary>
/// What a verification against the seal protects, in each situation (issue #62, part B1, CA-5): the words never claim integrity without qualifying it, the manual situation says the date
/// of the last seal or that there is none, and the other two say what they guard against and what they do not.
/// </summary>
public sealed class SealProtectionTests
{
    private static readonly DateTimeOffset Moment = new(2031, 3, 4, 5, 6, 7, TimeSpan.FromHours(-3));

    [Fact]
    public void ExternalSituationGuardsAgainstABaseReplacedAndAnEndRemovedAndNotAgainstWhoControlsTheDestination()
    {
        var words = SealProtection.Of(SealSituation.External, null);

        Assert.All(
            ["base substituída", "remoção do fim", "Não protege contra quem controla também esse destino", "declaração da instalação"],
            part => Assert.Contains(part, words, StringComparison.Ordinal));
    }

    [Fact]
    public void InternalSituationGuardsAgainstARestorationByMistakeAndWhoReachesOnlyTheDatabaseAndNotAgainstTheNetwork()
    {
        var words = SealProtection.Of(SealSituation.Internal, Moment);

        Assert.All(["restauração equivocada", "só o banco", "Não protege contra quem controla a rede", "declaração da instalação"], part => Assert.Contains(part, words, StringComparison.Ordinal));
    }

    [Fact]
    public void ManualSituationAttestsCoherenceAndNotIntegritySinceTheDateOfTheLastSealInUtc()
    {
        var words = SealProtection.Of(SealSituation.Manual, Moment);

        Assert.All(
            ["A comparação com o selo de 2031-03-04T08:06:07Z cobre a cadeia até essa data", "Desde ela", "só a coerência dos elos", "e não a integridade"],
            part => Assert.Contains(part, words, StringComparison.Ordinal));
        Assert.DoesNotContain("não foi conferido", words, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDateOfTheLastSealIsToldInWholeSecondsLikeTheTextOfASeal()
    {
        var words = SealProtection.Of(SealSituation.Manual, Moment.AddTicks(1_234_567));

        Assert.Contains("2031-03-04T08:06:07Z,", words, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualSituationWithNoSealSaysThereIsNoneAndStillAttestsNoIntegrity()
    {
        var words = SealProtection.Of(SealSituation.Manual, null);

        Assert.All(["Nenhum selo foi registrado ainda", "não atesta integridade"], part => Assert.Contains(part, words, StringComparison.Ordinal));
    }

    [Fact]
    public void ExternalAndInternalSituationsSayTheCoreDoesNotCheckTheDestinationAndThatItIsTheInstallationsDeclaration()
    {
        var words = new[] { SealSituation.External, SealSituation.Internal }.Select(situation => SealProtection.Of(situation, null));

        Assert.All(words, text => Assert.Contains("O núcleo não confere se o destino está fora do controle da base", text, StringComparison.Ordinal));
    }

    [Fact]
    public void ManualSituationDoesNotSayTheLinksSinceTheSealWereNotChecked() =>
        Assert.DoesNotContain("não foi conferido", SealProtection.Of(SealSituation.Manual, Moment), StringComparison.Ordinal);

    [Fact]
    public void NoSituationSaysTheChainMatchesTheSealSinceTheWordsSayWhatIsCoveredAndNotTheResult()
    {
        var all = Enum.GetValues<SealSituation>().Select(situation => SealProtection.Of(situation, Moment));

        Assert.All(all, words => Assert.DoesNotContain("confere com", words, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NoSituationAffirmsIntegrityWithoutQualifyingIt()
    {
        var all = Enum.GetValues<SealSituation>().Select(situation => SealProtection.Of(situation, null));

        Assert.All(all, words => Assert.DoesNotContain("garante a integridade", words, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ASituationOutsideTheDeclaredOnesHasNoWords() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SealProtection.Of((SealSituation)99, null));
}
