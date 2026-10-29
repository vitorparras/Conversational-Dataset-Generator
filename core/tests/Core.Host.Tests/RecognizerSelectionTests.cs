using Core.Boundary.Contracts;
using Core.Boundary.Fake;
using Core.Boundary.Http;
using Core.Host.Analysis;
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
    public void HttpKindWithNoServiceRunningPutsTheRecognizerThatRefusesInTheContainer()
    {
        var values = RecognizerContainer.Loaded("--Recognizer:Kind=http").Values;
        using var container = RecognizerContainer.Of(values, harness.SupervisorFor("zqx-missing-7731"), harness.Client);

        var recognizer = container.GetRequiredService<IRecognizer>();

        _ = Assert.IsType<StoppedServiceRecognizer>(recognizer);
    }

    [Fact]
    public void FakeKindReadsNoVersionFromAnyService()
    {
        var values = RecognizerContainer.Loaded("--Recognizer:Kind=fake").Values;
        using var container = RecognizerContainer.Of(values, harness.SupervisorFor("zqx-missing-7731"), harness.Client);

        _ = Assert.IsType<NoServiceVersionReader>(container.GetRequiredService<IServiceVersionReader>());
    }

    [Fact]
    public async Task HttpKindWithNoServiceRunningHasNoVersions()
    {
        var values = RecognizerContainer.Loaded("--Recognizer:Kind=http").Values;
        using var container = RecognizerContainer.Of(values, harness.SupervisorFor("zqx-missing-7731"), harness.Client);

        var versions = await container.GetRequiredService<IServiceVersionReader>().ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ServiceVersions.Absent, versions);
    }

    [Fact]
    public async Task HttpKindWithTheServiceRunningReadsItsVersionsAtItsPortWithItsSecret()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.SkipWhen(LanguageServiceInterpreter.Available is null, "Real service route skipped: uv could not resolve the language-service environment.");
        var supervisor = await harness.StartedSupervisorAsync(new ServiceProcessWatch(), cancellationToken);
        var values = RecognizerContainer.Loaded("--Recognizer:Kind=http").Values;
        using var container = RecognizerContainer.Of(values, supervisor, harness.Client);

        var reader = container.GetRequiredService<IServiceVersionReader>();

        _ = Assert.IsType<HttpServiceVersionReader>(reader);
        var versions = await reader.ReadAsync(cancellationToken);
        Assert.Equal(VersionValue.Present(ContractVersion.Current), versions.Contract);
        Assert.NotEqual(VersionValue.Absent, versions.Service);
        Assert.Equal(VersionValue.Absent, versions.Model);
    }

    public void Dispose() => harness.Dispose();
}
