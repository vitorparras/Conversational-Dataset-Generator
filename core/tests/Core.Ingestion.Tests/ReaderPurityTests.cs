using System.Reflection;
using Core.Ingestion.Reading;

namespace Core.Ingestion.Tests;

/// <summary>A reader has no state and no reach beyond the bytes it is given: no disk, no network, no clock, no environment, and no file name to read (CA-1).</summary>
public sealed class ReaderPurityTests
{
    private static readonly string[] Forbidden =
    [
        "System.IO", "System.Net", "File.", "Directory.", "Path.", "Environment.", "DateTime.Now", "DateTime.UtcNow", "DateTimeOffset.Now",
        "DateTimeOffset.UtcNow", "TimeProvider", "Stopwatch", "Process.", "Random", "Guid.NewGuid", "HttpClient", "Socket",
    ];

    [Fact]
    public void NoSourceOfTheReadersReachesDiskNetworkClockOrEnvironment()
    {
        var folder = Path.Combine(CoreRoot(), "src", "Core.Ingestion", "Reading");

        var found = Directory.EnumerateFiles(folder, "*.cs")
            .SelectMany(file => Forbidden.Where(word => File.ReadAllText(file).Contains(word, StringComparison.Ordinal)).Select(word => $"{Path.GetFileName(file)}: {word}"))
            .ToList();

        Assert.Empty(found);
    }

    [Fact]
    public void EveryReaderIsSealedAndKeepsNoInstanceState()
    {
        var readers = typeof(ISourceReader).Assembly.GetTypes().Where(type => type.IsClass && typeof(ISourceReader).IsAssignableFrom(type)).ToList();

        Assert.NotEmpty(readers);
        Assert.All(readers, reader => Assert.True(reader.IsSealed, reader.Name));
        Assert.All(readers, reader => Assert.Empty(reader.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)));
    }

    [Fact]
    public void ReadingTakesBytesAndNothingElseAndGivesTheOutcome()
    {
        var read = Assert.Single(typeof(ISourceReader).GetMethods(), method => !method.IsSpecialName);

        Assert.Equal(typeof(ReadOutcome), read.ReturnType);
        Assert.Equal([typeof(ReadOnlyMemory<byte>)], [.. read.GetParameters().Select(parameter => parameter.ParameterType)]);
        Assert.Equal(["Format"], [.. typeof(ISourceReader).GetProperties().Select(property => property.Name)]);
    }

    private static string CoreRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Core.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Core.slnx was not found above the test assembly.");
    }
}
