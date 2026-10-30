using System.Text.Json;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>A temporary directory under the machine's temp folder builds with the SDK the repository pins, whatever a <c>global.json</c> above it says.</summary>
public sealed class ScratchDirectoryTests
{
    [Fact]
    public void ScratchDirectoryHoldsTheGlobalJsonOfTheRepository()
    {
        using var scratch = ScratchDirectory.Create();

        var copy = File.ReadAllText(Path.Combine(scratch.Path, ScratchDirectory.GlobalJson));

        Assert.Equal(File.ReadAllText(ScratchDirectory.RootGlobalJson), copy);
    }

    [Fact]
    public async Task DotnetInTheScratchDirectoryAnswersWithTheSdkTheRepositoryPins()
    {
        using var scratch = ScratchDirectory.Create();

        var outcome = await DotnetCommand.RunAsync(["--version"], scratch.Path, TestContext.Current.CancellationToken);

        Assert.Equal(PinnedSdk(), outcome.OutputOfSuccess("dotnet --version").Trim());
    }

    private static string PinnedSdk()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ScratchDirectory.RootGlobalJson));
        return document.RootElement.GetProperty("sdk").GetProperty("version").GetString()!;
    }
}
