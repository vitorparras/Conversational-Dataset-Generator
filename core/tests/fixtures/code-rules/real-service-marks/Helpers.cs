namespace Core.Sample.Tests;

internal static class Starters
{
    public static Task<LoggedHost> StartAsync(bool realService) => LoggedHost.StartAsync(realService: realService, default);

    public static Task<LoggedHost> StartAsync() => LoggedHost.StartAsync(realService: false, default);
}
