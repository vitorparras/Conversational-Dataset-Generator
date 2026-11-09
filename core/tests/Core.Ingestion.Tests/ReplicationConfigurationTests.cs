using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;

namespace Core.Ingestion.Tests;

/// <summary>
/// The two values of the criterion live in the configuration of the domain (issue 82, criterion 5): each has a default, shows its origin in the effective configuration, is refused outside what makes
/// sense, and changing them changes the marks of the same material.
/// </summary>
public sealed class ReplicationConfigurationTests
{
    private static readonly DateTimeOffset Start = new(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NothingDeclaredGivesTheDefaultsAndShowsThemAsDefault()
    {
        var loaded = Loaded();

        Assert.Equal((20, 2), (ReplicationConfiguration.Rule(loaded.Values).MinLength, ReplicationConfiguration.Rule(loaded.Values).MinSenders));
        Assert.Contains(new EffectiveEntry("Replication:MinLength", "20", ConfigurationOrigin.Default), loaded.View.Entries);
        Assert.Contains(new EffectiveEntry("Replication:MinSenders", "2", ConfigurationOrigin.Default), loaded.View.Entries);
    }

    [Fact]
    public void AValueDeclaredShowsItsOriginAndTheOneNotDeclaredStaysTheDefault()
    {
        var loaded = Loaded("--Replication:MinLength=40");

        Assert.Equal(40, ReplicationConfiguration.Rule(loaded.Values).MinLength);
        Assert.Contains(new EffectiveEntry("Replication:MinLength", "40", ConfigurationOrigin.Argument), loaded.View.Entries);
        Assert.Contains(new EffectiveEntry("Replication:MinSenders", "2", ConfigurationOrigin.Default), loaded.View.Entries);
    }

    [Theory]
    [InlineData("Replication:MinLength", "0", "um número inteiro de caracteres")]
    [InlineData("Replication:MinLength", "-3", "um número inteiro de caracteres")]
    [InlineData("Replication:MinLength", "100001", "um número inteiro de caracteres")]
    [InlineData("Replication:MinLength", "vinte", "um número inteiro de caracteres")]
    [InlineData("Replication:MinSenders", "1", "um número inteiro de remetentes")]
    [InlineData("Replication:MinSenders", "0", "um número inteiro de remetentes")]
    [InlineData("Replication:MinSenders", "1001", "um número inteiro de remetentes")]
    [InlineData("Replication:MinSenders", "dois", "um número inteiro de remetentes")]
    public void AValueOutsideWhatMakesSenseIsRefusedNamingTheKeyTheValueAndWhatIsAccepted(string key, string declared, string accepted)
    {
        var problem = Assert.Single(ReplicationConfiguration.Refusals(Loaded($"--{key}={declared}").Values));

        Assert.Equal((ConfigurationProblemKind.ValueNotAccepted, key, declared), (problem.Kind, problem.Key, problem.Found));
        Assert.Contains(accepted, Assert.Single(problem.Accepted!), StringComparison.Ordinal);
    }

    [Fact]
    public void TheValuesAtTheLimitsOfWhatIsAcceptedAreAccepted()
    {
        Assert.Empty(ReplicationConfiguration.Refusals(Loaded("--Replication:MinLength=1", "--Replication:MinSenders=2").Values));
        Assert.Empty(ReplicationConfiguration.Refusals(Loaded("--Replication:MinLength=100000", "--Replication:MinSenders=1000").Values));
    }

    [Fact]
    public void TheSameMaterialWithOtherValuesGivesOtherMarks()
    {
        var material = new List<ReplicationOccurrence>
        {
            Said(1, "ZQX-curto", "ZQX-ana", 12, 0),
            Said(2, "ZQX-curto", "ZQX-bia", 12, 1),
            Said(3, "ZQX-longo", "ZQX-ana", 60, 2),
            Said(4, "ZQX-longo", "ZQX-bia", 60, 3),
            Said(5, "ZQX-longo", "ZQX-caio", 60, 4),
        };

        Assert.Equal([3L, 4, 5], MarkedBy(material));
        Assert.Equal([1L, 2, 3, 4, 5], MarkedBy(material, "--Replication:MinLength=5"));
        Assert.Equal([3L, 4, 5], MarkedBy(material, "--Replication:MinLength=5", "--Replication:MinSenders=3"));
        Assert.Empty(MarkedBy(material, "--Replication:MinLength=5", "--Replication:MinSenders=4"));
        Assert.Empty(MarkedBy(material, "--Replication:MinLength=61"));
    }

    private static IReadOnlyList<long> MarkedBy(IReadOnlyList<ReplicationOccurrence> material, params string[] arguments) =>
        [.. Replication.Marks(ReplicationConfiguration.Rule(Loaded(arguments).Values), [], material).Select(mark => mark.Of.Item).Order()];

    private static ReplicationOccurrence Said(long item, string text, string sender, int length, int minute) =>
        new(new ReplicationPlace(item, 0), text, sender, length, Start.AddMinutes(minute));

    private static ConfigurationLoadResult Loaded(params string[] arguments) =>
        ClosedConfiguration.Load(new ConfigurationSchema([ReplicationConfiguration.Section]), new StrictArgumentsSource(arguments));
}
