using Core.Platform.Keys;
using Core.Platform.Startup;

namespace Core.Platform.Tests.Keys;

/// <summary>The start is refused without a usable master key, the refusal says what is missing or what was found, and no value ever reaches it.</summary>
public sealed class MasterKeyCheckTests
{
    [Fact]
    public async Task KeyNotProvidedRefusesNamingWhatIsMissingAndWhereItShouldComeFrom()
    {
        var check = new MasterKeyCheck(FakeMasterKeySource.Lacking("o arquivo de custódia não foi informado"));

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Equal("MasterKey", result.Name);
        Assert.Contains("chave-mestra ausente: o arquivo de custódia não foi informado", result.Message, StringComparison.Ordinal);
        Assert.Equal("fonte de teste", result.ExpectedSource);
    }

    [Fact]
    public async Task KeyNotProvidedRefusesSayingThereIsNoDefault()
    {
        var check = new MasterKeyCheck(FakeMasterKeySource.Lacking("nada"));

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Contains("não tem valor padrão", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(64)]
    public async Task KeyOfOtherSizeRefusesNamingTheSizeFound(int length)
    {
        var check = new MasterKeyCheck(FakeMasterKeySource.Providing(SyntheticKey.OfLength(length)));

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains($"chave-mestra de tamanho inválido: encontrado {length} bytes, esperados 32", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeyThatCannotBeReadRefusesNamingWhatWasFound()
    {
        var check = new MasterKeyCheck(FakeMasterKeySource.Unreadable("texto que não é base64"));

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("chave-mestra ilegível: encontrado texto que não é base64", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefusalForTheSizeNeverReproducesTheValueInAnySpelling()
    {
        var key = SyntheticKey.OfLength(16);
        var check = new MasterKeyCheck(FakeMasterKeySource.Providing(key));

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.All(SyntheticKey.Spellings(key), spelling => Assert.DoesNotContain(spelling, result.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task KeyOfTheRightSizePassesWithoutShowingTheValue()
    {
        var key = SyntheticKey.Valid();
        var check = new MasterKeyCheck(FakeMasterKeySource.Providing(key));

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.Contains("chave-mestra de 32 bytes lida", result.Message, StringComparison.Ordinal);
        Assert.All(SyntheticKey.Spellings(key), spelling => Assert.DoesNotContain(spelling, result.Message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadingTheCheckTookIsReleasedWhenTheCheckEnds()
    {
        var released = new List<MasterKeyReading>();
        var check = new MasterKeyCheck(new RecordingSource(released));

        _ = await check.RunAsync(TestContext.Current.CancellationToken);

        _ = Assert.Throws<ObjectDisposedException>(() => released.Single().Key!.AsSpan().ToArray());
    }

    [Fact]
    public void CheckWithoutASourceIsADefect() =>
        _ = Assert.Throws<ArgumentNullException>(() => new MasterKeyCheck(null!));

    private sealed class RecordingSource(List<MasterKeyReading> given) : IMasterKeySource
    {
        public string Origin => "fonte de teste";

        public Task<MasterKeyReading> ReadAsync(CancellationToken cancellationToken)
        {
            var reading = MasterKeyReading.FromBytes(SyntheticKey.Valid(), DerivationLabels.None);
            given.Add(reading);
            return Task.FromResult(reading);
        }
    }
}
