using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Core.Host.Supervision;
using Core.Platform.Startup;
using Core.Tests.Integration.Host;

namespace Core.Tests.Integration.Supervision;

/// <summary>
/// Issue 29 CA-3 and CA-5, issue 32 CA-4: the supervisor starts the service with a secret that
/// lives nowhere but in memory of the two processes, and knows when the child is gone. Each test
/// that can run against both services does: the real one (needs uv) and the stand-in.
/// </summary>
public sealed partial class SupervisorTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(15);

    // No proxy: the text of a material never goes through one, and a test must not either.
    private static readonly HttpClient Http = new(new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(10) };

    public static TheoryData<string> Services => ["fake", "real"];

    [Theory]
    [MemberData(nameof(Services))]
    public async Task StartedServiceAnswersAuthenticatedRequests(string service)
    {
        await using var supervisor = Create(Setup(service), new CapturingLogger());

        var result = await supervisor.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.Equal("LanguageService", result.Name);
        Assert.True(supervisor.Availability.IsAvailable);
        using var request = supervisor.CreateRequest(HttpMethod.Get, "/health");
        using var response = await Http.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("127.0.0.1", request.RequestUri!.Host);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public async Task ArgumentsOfTheChildProcessDoNotHoldTheSecret(string service)
    {
        await using var supervisor = Create(Setup(service), new CapturingLogger());
        _ = await supervisor.RunAsync(TestContext.Current.CancellationToken);
        var secret = SecretOf(supervisor);

        var info = supervisor.LastStartInfo!;
        Assert.DoesNotContain(secret, info.FileName, StringComparison.Ordinal);
        Assert.All(info.ArgumentList, argument => Assert.DoesNotContain(secret, argument, StringComparison.Ordinal));
        Assert.Contains("--port", info.ArgumentList);
        Assert.True(info.RedirectStandardInput, "the secret must travel on standard input");

        // What the system reports to any account on the machine, for the child and its descendants.
        var lines = (await ProcessCommandLines.OfTreeAsync(supervisor.ProcessId)).Select(row => row.CommandLine).ToList();
        Assert.NotEmpty(lines);
        Assert.Contains(lines, line => line.Contains("--port", StringComparison.Ordinal));
        Assert.All(lines, line => Assert.DoesNotContain(secret, line, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Services))]
    public async Task NoFileHoldsTheSecretOnceTheServiceIsUp(string service)
    {
        var since = DateTime.UtcNow.AddMinutes(-1);
        await using var supervisor = Create(Setup(service), new CapturingLogger());
        _ = await supervisor.RunAsync(TestContext.Current.CancellationToken);
        var secret = SecretOf(supervisor);
        using var call = supervisor.CreateRequest(HttpMethod.Get, "/health");
        using var answer = await Http.SendAsync(call, TestContext.Current.CancellationToken);

        var files = SecretSweep.FilesHolding(secret, since);

        Assert.Empty(files);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public async Task NoLogMessageHoldsTheSecret(string service)
    {
        var logger = new CapturingLogger();
        await using var supervisor = Create(Setup(service), logger);
        _ = await supervisor.RunAsync(TestContext.Current.CancellationToken);
        var first = SecretOf(supervisor);
        _ = await supervisor.RestartAsync(TestContext.Current.CancellationToken);
        var second = SecretOf(supervisor);
        await supervisor.StopAsync();

        Assert.NotEmpty(logger.Messages);
        foreach (var message in logger.Messages)
        {
            Assert.DoesNotContain(first, message, StringComparison.Ordinal);
            Assert.DoesNotContain(second, message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(Services))]
    public async Task RestartMakesANewSecretAndTheServiceRefusesTheOldOne(string service)
    {
        await using var supervisor = Create(Setup(service), new CapturingLogger());
        _ = await supervisor.RunAsync(TestContext.Current.CancellationToken);
        var oldSecret = SecretOf(supervisor);

        var result = await supervisor.RestartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Passed, result.Status);
        var newSecret = SecretOf(supervisor);
        Assert.NotEqual(oldSecret, newSecret);

        // The old secret, sent to the service as it is now.
        using var current = supervisor.CreateRequest(HttpMethod.Get, "/health");
        using var stale = new HttpRequestMessage(HttpMethod.Get, current.RequestUri);
        stale.Headers.Add(LanguageServiceSupervisor.SecretHeader, oldSecret);
        using var refused = await Http.SendAsync(stale, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        using var accepted = await Http.SendAsync(current, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public async Task KilledChildMakesTheServiceUnavailableAndSaysWhy(string service)
    {
        await using var supervisor = Create(Setup(service), new CapturingLogger());
        _ = await supervisor.RunAsync(TestContext.Current.CancellationToken);

        using (var child = Process.GetProcessById(supervisor.ProcessId))
        {
            child.Kill(entireProcessTree: true);
        }

        await UntilAsync(() => !supervisor.Availability.IsAvailable);
        var availability = supervisor.Availability;
        Assert.False(availability.IsAvailable);
        Assert.Contains("encerrou com código", availability.Reason, StringComparison.Ordinal);
        Assert.Equal(CheckStatus.Failed, supervisor.Current().Status);
        var error = Assert.Throws<InvalidOperationException>(() => supervisor.CreateRequest(HttpMethod.Get, "/health"));
        Assert.Contains("indisponível", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public async Task StopEndsTheWholeProcessTree(string service)
    {
        await using var supervisor = Create(Setup(service), new CapturingLogger());
        _ = await supervisor.RunAsync(TestContext.Current.CancellationToken);
        var tree = (await ProcessCommandLines.OfTreeAsync(supervisor.ProcessId)).Select(row => row.Id).ToList();
        Assert.Contains(supervisor.ProcessId, tree);

        await supervisor.StopAsync();

        Assert.False(supervisor.Availability.IsAvailable);
        await UntilAsync(() => tree.All(id => !IsRunning(id)));
    }

    [Fact(Timeout = 60_000)]
    public async Task ServiceThatExitsOnStartFailsTheCheckWithItsExitCode()
    {
        await using var supervisor = Create(LanguageServiceSetup.Fake("exit"), new CapturingLogger());

        var result = await supervisor.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("código 7", result.Message, StringComparison.Ordinal);
        Assert.False(supervisor.Availability.IsAvailable);
    }

    [Fact(Timeout = 60_000)]
    public async Task ServiceThatNeverAnswersFailsTheCheckAtTheTimeoutAndIsEnded()
    {
        await using var supervisor = Create(LanguageServiceSetup.Fake("hang"), new CapturingLogger(), timeoutSeconds: 2);
        var watch = Stopwatch.StartNew();

        var result = await supervisor.RunAsync(TestContext.Current.CancellationToken);

        watch.Stop();
        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("não respondeu em 2 s", result.Message, StringComparison.Ordinal);
        Assert.Contains("LanguageService:StartupTimeoutSeconds", result.Message, StringComparison.Ordinal);
        Assert.InRange(watch.Elapsed, TimeSpan.FromSeconds(1.9), TimeSpan.FromSeconds(12));
        Assert.False(IsRunning(supervisor.ProcessId), "a service that never answered must not be left running");
    }

    [Fact]
    public async Task CommandThatCannotBeStartedFailsTheCheckWithoutShowingTheCommand()
    {
        const string Missing = "zqx-no-such-command-7731";
        await using var supervisor = Create(new LanguageServiceSetup(Missing, string.Empty), new CapturingLogger());

        var result = await supervisor.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("LanguageService:Command", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Missing, result.Message + result.ExpectedSource, StringComparison.Ordinal);
        Assert.False(supervisor.Availability.IsAvailable);
    }

    [Fact]
    public async Task ServiceNotStartedYetIsUnavailableAndRefusesToBuildARequest()
    {
        await using var supervisor = Create(LanguageServiceSetup.Fake(), new CapturingLogger());

        Assert.False(supervisor.Availability.IsAvailable);
        _ = Assert.Throws<InvalidOperationException>(() => supervisor.CreateRequest(HttpMethod.Get, "/health"));
        Assert.Equal(CheckStatus.Failed, supervisor.Current().Status);
    }

    [Fact]
    public async Task RefreshReplacesOnlyTheEntryOfTheService()
    {
        await using var supervisor = Create(LanguageServiceSetup.Fake(), new CapturingLogger());
        var startup = new StartupReport(
        [
            CheckResult.Pass("other", "presente", "origem"),
            await supervisor.RunAsync(TestContext.Current.CancellationToken),
        ]);
        await supervisor.StopAsync();

        var live = supervisor.Refresh(startup);

        Assert.Equal(CheckStatus.Passed, live.Results[0].Status);
        Assert.Equal(CheckStatus.Failed, live.Results[1].Status);
        Assert.False(live.AllPassed);
    }

    [Fact]
    public void EachSecretIs32RandomBytesAsHexadecimal()
    {
        var secrets = Enumerable.Range(0, 50).Select(_ => LanguageServiceSupervisor.NewSecret()).ToList();

        Assert.All(secrets, secret => Assert.Matches(Hex64(), secret));
        Assert.Equal(secrets.Count, secrets.Distinct(StringComparer.Ordinal).Count());
    }

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Hex64();

    private static LanguageServiceSetup Setup(string service)
    {
        if (service != "real")
        {
            return LanguageServiceSetup.Fake();
        }

        Assert.SkipUnless(LanguageServiceSetup.UvAvailable(), LanguageServiceSetup.UvMissing);
        return LanguageServiceSetup.Real();
    }

    private static LanguageServiceSupervisor Create(LanguageServiceSetup setup, CapturingLogger logger, int timeoutSeconds = 60) =>
        new(
            new LanguageServiceOptions(
                setup.Command,
                [.. setup.Arguments.Split(LanguageServiceOptions.ArgumentSeparator, StringSplitOptions.RemoveEmptyEntries)],
                TimeSpan.FromSeconds(timeoutSeconds)),
            logger);

    private static string SecretOf(LanguageServiceSupervisor supervisor)
    {
        using var request = supervisor.CreateRequest(HttpMethod.Get, "/health");
        return request.Headers.GetValues(LanguageServiceSupervisor.SecretHeader).Single();
    }

    private static bool IsRunning(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var waited = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(waited.Elapsed < Wait, "the condition did not come true in time");
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }
}
