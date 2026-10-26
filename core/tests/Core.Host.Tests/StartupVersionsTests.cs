using System.Collections.Concurrent;
using System.Globalization;
using Core.Boundary.Fake;
using Core.Boundary.Http;
using Core.Platform.Versions;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests;

/// <summary>The startup line carries the versions the service declares, each absent on its own account, and is written once with what the service said at that moment.</summary>
public sealed class StartupVersionsTests
{
    private static readonly ServiceVersions Declared = new(VersionValue.Present("7"), VersionValue.Present("0.4.2"), VersionValue.Present("pt-model-3.2"));

    private static readonly VersionReport Report = new(new BuildInfo(
        VersionValue.Present("9.8.7"), VersionValue.Present("0123456789abcdef0123456789abcdef01234567"), WorkingTree.Clean));

    [Fact]
    public async Task ServiceThatDeclaresVersionsHasThemInTheLineWithItsEventId()
    {
        var log = new CapturedVersionsLog();

        await StartupVersions.WriteAsync(
            log, Report, new ScriptedReader(Declared), TestContext.Current.CancellationToken);

        var line = Assert.Single(log.Lines);
        Assert.Equal(200, line.EventId);
        Assert.Equal(["7", "0.4.2", "pt-model-3.2"], [line.Fields["ContractVersion"], line.Fields["LanguageServiceVersion"], line.Fields["ModelVersion"]]);
    }

    [Fact]
    public async Task ServiceThatGaveNothingHasAllThreeAbsentInTheLine()
    {
        var log = new CapturedVersionsLog();

        await StartupVersions.WriteAsync(log, Report, new ScriptedReader(ServiceVersions.Absent), TestContext.Current.CancellationToken);

        var line = Assert.Single(log.Lines);
        Assert.Equal(
            [VersionValue.Absent.ToString(), VersionValue.Absent.ToString(), VersionValue.Absent.ToString()],
            [line.Fields["ContractVersion"], line.Fields["LanguageServiceVersion"], line.Fields["ModelVersion"]]);
    }

    [Fact]
    public async Task OneVersionTheServiceDidNotGiveIsAbsentWhileTheOthersStay()
    {
        var log = new CapturedVersionsLog();
        var versions = new ServiceVersions(VersionValue.Present("7"), VersionValue.Absent, VersionValue.Present("pt-model-3.2"));

        await StartupVersions.WriteAsync(log, Report, new ScriptedReader(versions), TestContext.Current.CancellationToken);

        var line = Assert.Single(log.Lines);
        Assert.Equal(["7", VersionValue.Absent.ToString(), "pt-model-3.2"], [line.Fields["ContractVersion"], line.Fields["LanguageServiceVersion"], line.Fields["ModelVersion"]]);
    }

    [Fact]
    public async Task FakeRecognizerMakesNoConnectionAndHasThreeAbsentVersions()
    {
        var log = new CapturedVersionsLog();

        await StartupVersions.WriteAsync(log, Report, new NoServiceVersionReader(), TestContext.Current.CancellationToken);

        var line = Assert.Single(log.Lines);
        Assert.Equal(
            [VersionValue.Absent.ToString(), VersionValue.Absent.ToString(), VersionValue.Absent.ToString()],
            [line.Fields["ContractVersion"], line.Fields["LanguageServiceVersion"], line.Fields["ModelVersion"]]);
    }

    [Fact]
    public async Task LineKeepsTheBuildAndAsksTheServiceOnceWithoutChangingTheReport()
    {
        var log = new CapturedVersionsLog();
        var reader = new ScriptedReader(Declared);

        await StartupVersions.WriteAsync(log, Report, reader, TestContext.Current.CancellationToken);

        var line = Assert.Single(log.Lines);
        Assert.Equal("9.8.7", line.Fields["CoreVersion"]);
        Assert.Equal(1, reader.Reads);
        Assert.Equal(VersionValue.Absent, Report.ModelVersion);
    }

    private sealed class ScriptedReader(ServiceVersions answer) : IServiceVersionReader
    {
        private int reads;

        public int Reads => reads;

        public Task<ServiceVersions> ReadAsync(CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref reads);
            return Task.FromResult(answer);
        }
    }

    private sealed record Line(int EventId, IReadOnlyDictionary<string, string?> Fields);

    /// <summary>Keeps every line written with its event id and each field beside the message.</summary>
    private sealed class CapturedVersionsLog : ILogger
    {
        private readonly ConcurrentQueue<Line> lines = new();

        public IReadOnlyList<Line> Lines => [.. lines];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var fields = state as IEnumerable<KeyValuePair<string, object?>> ?? [];
            lines.Enqueue(new Line(
                eventId.Id,
                fields.ToDictionary(field => field.Key, field => Convert.ToString(field.Value, CultureInfo.InvariantCulture))));
        }
    }
}
