namespace Core.Platform.Keys;

/// <summary>
/// The custody files as the means of delivery: one holds the key, as base64 on a line, another the labels, one per line, and the third the epochs, one per
/// line as a number and a secret in base64 (decision 0049). All three are refused when absent, when open to anyone besides the process's owner and when inside the artifact's folder; what the operator
/// reads names the file and what was found, never the content. The paths appear only in the refusal of the start, which serves no
/// route; what a passed check shows names the configuration keys instead.
/// </summary>
public sealed class FileMasterKeySource : IMasterKeySource
{
    private readonly CustodyFile key;
    private readonly CustodyFile labels;
    private readonly CustodyFile epochs;
    private readonly string artifactDirectory;

    /// <summary>The source of the three files, refusing any that lies inside <paramref name="artifactDirectory"/>.</summary>
    public FileMasterKeySource(string keyPath, string labelsPath, string epochsPath, string artifactDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(epochsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactDirectory);
        key = new CustodyFile("arquivo da chave", Path.GetFullPath(keyPath), CustodyFileFormat.LongestKeyFile);
        labels = new CustodyFile("arquivo dos rótulos", Path.GetFullPath(labelsPath), CustodyFileFormat.LongestLabelsFile);
        epochs = new CustodyFile("arquivo das épocas", Path.GetFullPath(epochsPath), CustodyFileFormat.LongestEpochsFile);
        this.artifactDirectory = Path.GetFullPath(artifactDirectory);
    }

    /// <summary>Names the three configuration keys that point at the files, not the paths, which are not for the status route to show.</summary>
    public string Origin => "arquivos de custódia, nomeados por MasterKey:File, MasterKey:LabelsFile e MasterKey:EpochsFile";

    /// <inheritdoc />
    public async Task<MasterKeyReading> ReadAsync(CancellationToken cancellationToken)
    {
        var absent = new[] { key, labels, epochs }.Where(file => !File.Exists(file.Path)).Select(file => $"{file.Name} não encontrado em '{file.Path}'").ToList();
        if (absent.Count > 0)
        {
            return MasterKeyReading.Absent(string.Join("; ", absent));
        }

        var refusal = Refusal(key) ?? Refusal(labels) ?? Refusal(epochs);
        if (refusal is not null)
        {
            return MasterKeyReading.Unreadable(refusal);
        }

        return await ReadFilesAsync(cancellationToken).ConfigureAwait(false);
    }

    private string? Refusal(CustodyFile file)
    {
        try
        {
            var real = RealPath.Of(file.Path);
            if (RealPath.IsWithin(real, artifactDirectory))
            {
                return $"{file.Name} em '{file.Path}', dentro da pasta do artefato; ele fica fora dela";
            }

            return KeyFilePermission.Excess(real) is { } excess ? $"{file.Name} em '{file.Path}': {excess}" : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Unopened(file, exception);
        }
    }

    private async Task<MasterKeyReading> ReadFilesAsync(CancellationToken cancellationToken)
    {
        var (labelsContent, labelsReading) = await ContentAsync(labels, cancellationToken).ConfigureAwait(false);
        if (labelsContent is null)
        {
            return MasterKeyReading.Unreadable(labelsReading!);
        }

        var (given, labelsProblem) = CustodyFileFormat.ParseLabels(labelsContent);
        if (given is null)
        {
            return MasterKeyReading.Unreadable($"{labels.Name} em '{labels.Path}': {labelsProblem}");
        }

        var (loaded, epochsProblem) = await EpochsAsync(cancellationToken).ConfigureAwait(false);
        if (loaded is null)
        {
            return MasterKeyReading.Unreadable(epochsProblem!);
        }

        var reading = await KeyAsync(given, loaded, cancellationToken).ConfigureAwait(false);
        if (reading.Failure is not null)
        {
            loaded.Dispose();
        }

        return reading;
    }

    private async Task<(CustodyEpochs? Epochs, string? Problem)> EpochsAsync(CancellationToken cancellationToken)
    {
        var (content, reading) = await ContentAsync(epochs, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            return (null, reading);
        }

        var (parsed, problem) = CustodyFileFormat.ParseEpochs(content);
        return parsed is null ? (null, $"{epochs.Name} em '{epochs.Path}': {problem}") : (parsed, null);
    }

    private async Task<MasterKeyReading> KeyAsync(DerivationLabels given, CustodyEpochs loaded, CancellationToken cancellationToken)
    {
        var (keyContent, keyReading) = await ContentAsync(key, cancellationToken).ConfigureAwait(false);
        if (keyContent is null)
        {
            return MasterKeyReading.Unreadable(keyReading!);
        }

        var (bytes, keyProblem) = CustodyFileFormat.ParseKey(keyContent);
        if (bytes is null)
        {
            return MasterKeyReading.Unreadable($"{key.Name} em '{key.Path}': {keyProblem}");
        }

        return MasterKeyReading.FromBytes(bytes, given, loaded, $"{key.Name} em '{key.Path}'");
    }

    /// <summary>What the file holds, or why it was not read, naming the file.</summary>
    private static async Task<(byte[]? Content, string? Problem)> ContentAsync(CustodyFile file, CancellationToken cancellationToken)
    {
        try
        {
            return await ReadWithinLimitAsync(file, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, Unopened(file, exception));
        }
    }

    /// <summary>The content, read through the one handle that also tells the size, unless the file is larger than its kind can be.</summary>
    private static async Task<(byte[]? Content, string? Problem)> ReadWithinLimitAsync(CustodyFile file, CancellationToken cancellationToken)
    {
        var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            if (stream.Length > file.Longest)
            {
                return (null, $"{file.Name} em '{file.Path}': maior que {file.Longest} bytes, o que um arquivo desse tipo não é");
            }

            var content = new byte[stream.Length];
            await stream.ReadExactlyAsync(content, cancellationToken).ConfigureAwait(false);
            return (content, null);
        }
    }

    private static string Unopened(CustodyFile file, Exception exception) =>
        $"{file.Name} em '{file.Path}': não pôde ser aberto ({exception.GetType().Name})";

    private sealed record CustodyFile(string Name, string Path, int Longest);
}
