using Core.Platform.Keys;
using Core.Platform.Startup;

namespace Core.Host.Tests;

/// <summary>What the operator reads when the start is refused for the key: what is missing or what was found, and never the value, in any spelling.</summary>
public sealed class MasterKeyRefusalTests
{
    private static readonly byte[] Synthetic = [.. Enumerable.Range(0, 16).Select(index => (byte)(0xC3 ^ ((index * 29) + 7)))];

    [Fact]
    public async Task RefusalForAKeyOfTheWrongSizeNamesTheSizeAndNeverTheValue()
    {
        var output = await RefusalOfAsync(new SourceOf(MasterKeyReading.FromBytes([.. Synthetic], DerivationLabels.None)));

        Assert.Contains("chave-mestra de tamanho inválido: encontrado 16 bytes, esperados 32", output, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToHexString(Synthetic), output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Convert.ToBase64String(Synthetic), output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefusalForAKeyNotProvidedNamesWhatIsMissing()
    {
        var output = await RefusalOfAsync(new SourceOf(MasterKeyReading.Absent("o arquivo de custódia não foi informado")));

        Assert.Contains("chave-mestra ausente: o arquivo de custódia não foi informado", output, StringComparison.Ordinal);
    }

    private static async Task<string> RefusalOfAsync(IMasterKeySource source)
    {
        var report = await new StartupRunner([new MasterKeyCheck(source)], TimeProvider.System).RunAsync(TestContext.Current.CancellationToken);
        using var output = new StringWriter();
        _ = StartupRefusal.Report(output, report);
        return output.ToString();
    }

    private sealed class SourceOf(MasterKeyReading reading) : IMasterKeySource
    {
        public string Origin => "fonte de teste";

        public Task<MasterKeyReading> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(reading);
    }
}
