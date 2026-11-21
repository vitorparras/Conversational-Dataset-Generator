using Core.Persistence.Content;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Content.Writing;

namespace Core.Persistence.Tests.Content;

/// <summary>What the rerun carries of an item, shown in a log or a failure, gives versions and counts and never a word of a text nor the trail back to the raw one.</summary>
public sealed class RerunShownTests
{
    private const string Raw = "ZQXBRUTO texto bruto";
    private const string Held = "ZQXGUARDADO texto guardado";
    private const string Text = "ZQXNOVO texto novo";
    private const string Trail = /*lang=json*/ """{"raw":20,"steps":[["ZQXRASTRO"]]}""";

    [Fact]
    public void WhatTheRerunProducedShowsNeitherTheTextNorTheTrail()
    {
        var shown = new Resanitized(Text, RerunCase.Target, RerunCase.TargetSteps, Trail, [1, 0], [0, 2], Identical: false).ToString();

        Assert.DoesNotContain("ZQX", shown, StringComparison.Ordinal);
        Assert.Contains(RerunCase.Target, shown, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatTheRerunWorksFromShowsNoText()
    {
        var shown = new RerunSource(Raw, [new ItemOccurrence(0, 8, "PERSON", OccurrenceDestination.Remove)], Held, RerunCase.Target).ToString();

        Assert.DoesNotContain("ZQX", shown, StringComparison.Ordinal);
        Assert.Contains("Occurrences = 1", shown, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSanitizationOfAnItemShowsNeitherTheTextNorTheTrail()
    {
        var shown = new ItemSanitization { Text = Text, Version = RerunCase.Target, Steps = RerunCase.TargetSteps, Trail = Trail, Counts = [1, 0], Untreated = [0, 2] }.ToString();

        Assert.DoesNotContain("ZQX", shown, StringComparison.Ordinal);
        Assert.Contains(RerunCase.Target, shown, StringComparison.Ordinal);
    }
}
