using Core.Platform.Keys;
using Core.Platform.Startup;
using Core.Tests.Support.Custody;

namespace Core.Platform.Tests.Keys;

/// <summary>The custody files as the means of delivery: what they give when they are right, and what each refusal says, which is never the key.</summary>
public sealed class FileMasterKeySourceTests : IDisposable
{
    private readonly CustodyFiles files = new();

    public void Dispose() => files.Dispose();

    [Fact]
    public async Task FilesOfTheRightFormGiveTheKeyAndTheLabels()
    {
        var source = Source(KeyLine(SyntheticKey.Valid()), "dominio-b\ndominio-a\n");

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Null(reading.Failure);
        Assert.True(reading.Key!.AsSpan().SequenceEqual(SyntheticKey.Valid()));
        Assert.Equal(["dominio-a", "dominio-b"], reading.Labels.All);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task KeyLineWithOrWithoutItsBreakGivesTheSameKey(string ending)
    {
        var source = Source(Convert.ToBase64String(SyntheticKey.Valid()) + ending, string.Empty);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Null(reading.Failure);
        Assert.True(reading.Key!.AsSpan().SequenceEqual(SyntheticKey.Valid()));
    }

    [Fact]
    public async Task OriginNamesTheConfigurationKeysAndNoPath()
    {
        var source = Source(KeyLine(SyntheticKey.Valid()), string.Empty);

        Assert.Contains("MasterKey:File", source.Origin, StringComparison.Ordinal);
        Assert.Contains("MasterKey:LabelsFile", source.Origin, StringComparison.Ordinal);
        Assert.DoesNotContain(files.Directory, source.Origin, StringComparison.Ordinal);
        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Null(reading.Failure);
    }

    [Fact]
    public async Task KeyFileAbsentRefusesNamingTheFileAndItsPath()
    {
        var source = new FileMasterKeySource(Path.Combine(files.Directory, "sem-chave"), files.WriteText("labels", string.Empty), AppContext.BaseDirectory);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MasterKeyFailureKind.Absent, reading.Failure!.Kind);
        Assert.Contains($"arquivo da chave não encontrado em '{Path.Combine(files.Directory, "sem-chave")}'", reading.Failure.Found, StringComparison.Ordinal);
        Assert.DoesNotContain("rótulos", reading.Failure.Found, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LabelsFileAbsentRefusesNamingTheFileAndItsPath()
    {
        var source = new FileMasterKeySource(files.WriteText("key", KeyLine(SyntheticKey.Valid())), Path.Combine(files.Directory, "sem-rotulos"), AppContext.BaseDirectory);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MasterKeyFailureKind.Absent, reading.Failure!.Kind);
        Assert.Contains($"arquivo dos rótulos não encontrado em '{Path.Combine(files.Directory, "sem-rotulos")}'", reading.Failure.Found, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BothFilesAbsentRefuseNamingBoth()
    {
        var source = new FileMasterKeySource(Path.Combine(files.Directory, "a"), Path.Combine(files.Directory, "b"), AppContext.BaseDirectory);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Contains("arquivo da chave não encontrado", reading.Failure!.Found, StringComparison.Ordinal);
        Assert.Contains("arquivo dos rótulos não encontrado", reading.Failure.Found, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeyOfTheWrongSizeRefusesNamingTheSizeAndNeverTheValue()
    {
        var key = SyntheticKey.OfLength(16);
        var source = Source(KeyLine(key), string.Empty);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MasterKeyFailureKind.WrongSize, reading.Failure!.Kind);
        Assert.Equal($"arquivo da chave em '{Path.Combine(files.Directory, "key")}': 16 bytes, esperados 32", reading.Failure.Found);
        Assert.All(SyntheticKey.Spellings(key), spelling => Assert.DoesNotContain(spelling, reading.Failure.Found, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("isto-nao-e-base64!", "não é base64, 18 bytes")]
    [InlineData("", "arquivo vazio")]
    [InlineData("\n", "arquivo vazio")]
    public async Task KeyFileThatIsNotAKeyRefusesSayingWhatItFound(string content, string found)
    {
        var source = Source(content, string.Empty);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MasterKeyFailureKind.Unreadable, reading.Failure!.Kind);
        Assert.EndsWith($": {found}", reading.Failure.Found, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeyFileWithMoreThanOneLineRefusesNamingTheCountAndNeverTheLines()
    {
        var line = KeyLine(SyntheticKey.Valid()).TrimEnd()[..14];
        var source = Source($"{line}\n{line}\n{line}\n", string.Empty);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MasterKeyFailureKind.Unreadable, reading.Failure!.Kind);
        Assert.EndsWith(": 3 linhas, esperada 1", reading.Failure.Found, StringComparison.Ordinal);
        Assert.DoesNotContain(line, reading.Failure.Found, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeyFileMuchLargerThanAKeyRefusesWithoutReadingIt()
    {
        var source = Source(new string('A', CustodyFileFormat.LongestKeyFile + 1), string.Empty);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MasterKeyFailureKind.Unreadable, reading.Failure!.Kind);
        Assert.Contains($"arquivo da chave em '{Path.Combine(files.Directory, "key")}': maior que {CustodyFileFormat.LongestKeyFile} bytes", reading.Failure.Found, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LabelsFileMuchLargerThanLabelsRefusesWithoutReadingIt()
    {
        var source = Source(KeyLine(SyntheticKey.Valid()), new string('a', CustodyFileFormat.LongestLabelsFile + 1));

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MasterKeyFailureKind.Unreadable, reading.Failure!.Kind);
        Assert.Contains($"arquivo dos rótulos em '{Path.Combine(files.Directory, "labels")}': maior que {CustodyFileFormat.LongestLabelsFile} bytes", reading.Failure.Found, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LabelsFileThatCannotBeReadRefusesNamingTheFileAndTheLine()
    {
        var source = Source(KeyLine(SyntheticKey.Valid()), "dominio-a\n\ndominio-b\n");

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MasterKeyFailureKind.Unreadable, reading.Failure!.Kind);
        Assert.Contains($"arquivo dos rótulos em '{Path.Combine(files.Directory, "labels")}': linha 2", reading.Failure.Found, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeyFileInsideTheArtifactFolderRefusesNamingTheFileAndTheFolder()
    {
        var source = new FileMasterKeySource(files.WriteText("key", KeyLine(SyntheticKey.Valid())), files.WriteText("labels", string.Empty), files.Directory);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MasterKeyFailureKind.Unreadable, reading.Failure!.Kind);
        Assert.Contains($"arquivo da chave em '{Path.Combine(files.Directory, "key")}', dentro da pasta do artefato", reading.Failure.Found, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LabelsFileInsideTheArtifactFolderRefusesToo()
    {
        using var elsewhere = new CustodyFiles();
        var source = new FileMasterKeySource(elsewhere.WriteText("key", KeyLine(SyntheticKey.Valid())), files.WriteText("labels", string.Empty), files.Directory);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Contains($"arquivo dos rótulos em '{Path.Combine(files.Directory, "labels")}', dentro da pasta do artefato", reading.Failure!.Found, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FileInASiblingFolderWithTheSamePrefixIsNotInsideTheArtifactFolder()
    {
        var source = new FileMasterKeySource(files.WriteText("key", KeyLine(SyntheticKey.Valid())), files.WriteText("labels", string.Empty), files.Directory + "-irmao");

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Null(reading.Failure);
    }

    [Fact]
    public async Task LinkInsideTheArtifactFolderThatPointsOutsideIsNotInsideIt()
    {
        using var artifact = new CustodyFiles();
        var link = Path.Combine(artifact.Directory, "elo");
        Assert.SkipUnless(TryLink(link, files.WriteText("key", KeyLine(SyntheticKey.Valid()))), "a plataforma não deixou criar o elo simbólico");
        var source = new FileMasterKeySource(link, files.WriteText("labels", string.Empty), artifact.Directory);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Null(reading.Failure);
    }

    [Fact]
    public async Task LinkOutsideTheArtifactFolderThatPointsInsideRefusesNamingTheLink()
    {
        using var artifact = new CustodyFiles();
        var link = Path.Combine(files.Directory, "elo");
        Assert.SkipUnless(TryLink(link, artifact.WriteText("key", KeyLine(SyntheticKey.Valid()))), "a plataforma não deixou criar o elo simbólico");
        var source = new FileMasterKeySource(link, files.WriteText("labels", string.Empty), artifact.Directory);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Contains($"arquivo da chave em '{link}', dentro da pasta do artefato", reading.Failure!.Found, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FileGivenWithDotDotSegmentsInsideTheArtifactFolderIsRefused()
    {
        using var artifact = new CustodyFiles();
        _ = Directory.CreateDirectory(Path.Combine(artifact.Directory, "sub"));
        _ = artifact.WriteText("key", KeyLine(SyntheticKey.Valid()));
        var source = new FileMasterKeySource(Path.Combine(artifact.Directory, "sub", "..", "key"), files.WriteText("labels", string.Empty), artifact.Directory);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Contains("dentro da pasta do artefato", reading.Failure!.Found, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PassedCheckShowsNoPathOfTheCustodyFiles()
    {
        var source = Source(KeyLine(SyntheticKey.Valid()), "dominio-a\n");

        var result = await new MasterKeyCheck(source).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Passed, result.Status);
        Assert.DoesNotContain(files.Directory, result.Message + result.ExpectedSource + result.Observed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailureOfTheSizeNamesTheKeyFileAndItsPath()
    {
        var source = Source(KeyLine(SyntheticKey.OfLength(16)), string.Empty);

        var result = await new MasterKeyCheck(source).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            $"chave-mestra de tamanho inválido: encontrado arquivo da chave em '{Path.Combine(files.Directory, "key")}': 16 bytes, esperados 32",
            result.Message);
    }

    [Fact]
    public async Task FileThatCannotBeOpenedRefusesNamingTheKindOfFailureAndNoContent()
    {
        var source = Source(KeyLine(SyntheticKey.Valid()), string.Empty);
        await using var held = new FileStream(Path.Combine(files.Directory, "key"), FileMode.Open, FileAccess.Read, FileShare.None);

        using var reading = await source.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MasterKeyFailureKind.Unreadable, reading.Failure!.Kind);
        Assert.Contains($"arquivo da chave em '{Path.Combine(files.Directory, "key")}': não pôde ser aberto (IOException)", reading.Failure.Found, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceWithoutAPathIsADefect()
    {
        _ = Assert.Throws<ArgumentException>(() => new FileMasterKeySource(" ", "b", "c"));
        _ = Assert.Throws<ArgumentException>(() => new FileMasterKeySource("a", " ", "c"));
        _ = Assert.Throws<ArgumentException>(() => new FileMasterKeySource("a", "b", " "));
    }

    private static string KeyLine(byte[] key) => Convert.ToBase64String(key) + "\n";

    private static bool TryLink(string link, string target)
    {
        try
        {
            _ = File.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private FileMasterKeySource Source(string key, string labels) =>
        new(files.WriteText("key", key), files.WriteText("labels", labels), AppContext.BaseDirectory);
}
