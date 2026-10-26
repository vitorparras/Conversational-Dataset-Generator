using Core.Boundary.Contracts;
using Core.Boundary.Fake;
using Core.Boundary.Http;
using Core.Host.Supervision;
using Core.Host.Tests.Supervision;
using Core.Platform.Configuration;
using Core.Platform.Versions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests;

/// <summary>The recognizer is chosen by the key Recognizer:Kind, fake or http, exactly as written; the real one unless the fake is named.</summary>
public sealed class RecognizerSelectionTests : IDisposable
{
    private readonly SupervisionHarness harness = new();

    [Fact]
    public void RealRecognizerIsChosenWhenNothingDeclaresTheKind()
    {
        var loaded = RecognizerContainer.Loaded();

        Assert.False(RecognizerConfiguration.UsesFake(loaded.Values));
    }

    [Fact]
    public void KindNobodyDeclaredIsShownAsTheDefault()
    {
        var loaded = RecognizerContainer.Loaded();

        Assert.Contains(new EffectiveEntry("Recognizer:Kind", "http", ConfigurationOrigin.Default), loaded.View.Entries);
    }

    [Fact]
    public void FakeIsChosenByItsName()
    {
        var loaded = RecognizerContainer.Loaded("--Recognizer:Kind=fake");

        Assert.True(RecognizerConfiguration.UsesFake(loaded.Values));
    }

    [Theory]
    [InlineData("Fake")]
    [InlineData("FAKE")]
    [InlineData("Http")]
    [InlineData("real")]
    public void KindOutsideTheSetIsRefusedNamingTheKeyTheValueAndTheAcceptedOnes(string declared)
    {
        var loaded = RecognizerContainer.Loaded($"--Recognizer:Kind={declared}");

        Assert.Equal(
            $"valor não aceito para 'Recognizer:Kind': encontrado '{declared}'; aceitos: fake, http",
            Assert.Single(loaded.Problems).Describe());
    }

    [Fact]
    public void FakeKindPutsTheFakeRecognizerInTheContainer()
    {
        var values = RecognizerContainer.Loaded("--Recognizer:Kind=fake").Values;
        using var container = RecognizerContainer.Of(values, harness.SupervisorFor("zqx-missing-7731"), harness.Client);

        var recognizer = container.GetRequiredService<IRecognizer>();

        _ = Assert.IsType<FakeRecognizer>(recognizer);
    }

    [Fact]
    public void HttpKindAsksTheSupervisedServiceForItsPort()
    {
        var values = RecognizerContainer.Loaded("--Recognizer:Kind=http").Values;
        using var container = RecognizerContainer.Of(values, harness.SupervisorFor("zqx-missing-7731"), harness.Client);

        _ = Assert.Throws<InvalidOperationException>(container.GetRequiredService<IRecognizer>);
    }

    [Fact]
    public void FakeKindReadsNoModelVersionFromAnyService()
    {
        var values = RecognizerContainer.Loaded("--Recognizer:Kind=fake").Values;
        using var container = RecognizerContainer.Of(values, harness.SupervisorFor("zqx-missing-7731"), harness.Client);

        _ = Assert.IsType<NoModelVersionReader>(container.GetRequiredService<IModelVersionReader>());
    }

    [Fact]
    public async Task HttpKindWithNoServiceRunningHasNoModelVersion()
    {
        var values = RecognizerContainer.Loaded("--Recognizer:Kind=http").Values;
        using var container = RecognizerContainer.Of(values, harness.SupervisorFor("zqx-missing-7731"), harness.Client);

        var version = await container.GetRequiredService<IModelVersionReader>().ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(VersionValue.Absent, version);
    }

    [Fact]
    public async Task HttpKindWithTheServiceRunningReadsItsModelVersionAtItsPortWithItsSecret()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.SkipWhen(LanguageServiceInterpreter.Available is null, "Real service route skipped: uv could not resolve the language-service environment.");
        var supervisor = await harness.StartedSupervisorAsync(new ServiceProcessWatch(), cancellationToken);
        var values = RecognizerContainer.Loaded("--Recognizer:Kind=http").Values;
        using var container = RecognizerContainer.Of(values, supervisor, harness.Client);

        var reader = container.GetRequiredService<IModelVersionReader>();

        _ = Assert.IsType<HttpModelVersionReader>(reader);
        Assert.Equal(VersionValue.Absent, await reader.ReadAsync(cancellationToken));
    }

    public void Dispose() => harness.Dispose();
}
