namespace Core.Sample.Tests;

public sealed class UnmarkedTests
{
    [Fact]
    public async Task StartsTheRealHostDirectly() => _ = await HostProcess.StartWithTheRealServiceAsync(default);

    [Fact]
    public void InstallsTheRealServiceInTheHost() => _ = HostInstallation.Built(null) with { UsesRealService = true };

    [Fact]
    public async Task AsksTheLoggedHostForTheRealService() => _ = await LoggedHost.StartAsync(realService: true, default);

    [LinuxFact]
    public async Task ReachesTheRealServiceThroughAHelper() => _ = await HelperHoldingTheRealService();

    [Fact]
    public void NeedsTheInterpreterUvResolves() => _ = LanguageServiceInterpreter.Path;

    [Fact]
    public async Task ReachesTheRealServiceThroughTwoHelpers() => _ = await OuterHelper();

    private static Task<HostProcess> HelperHoldingTheRealService() => HostProcess.StartWithTheRealServiceAsync(default);

    private static Task<HostProcess> OuterHelper() => HelperHoldingTheRealService();
}
