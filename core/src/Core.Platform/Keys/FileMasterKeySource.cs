namespace Core.Platform.Keys;

/// <summary>
/// The custody files as the means of delivery: one holds the key, as base64 on a line, and the other the labels, one per line. Both
/// are refused when absent, when open to anyone besides the process's owner and when inside the artifact's folder; what the operator
/// reads names the file and what was found, never the content.
/// </summary>
public sealed class FileMasterKeySource : IMasterKeySource
{
    private readonly CustodyFile key;
    private readonly CustodyFile labels;
    private readonly string artifactDirectory;

    /// <summary>The source of the two files, refusing any that lies inside <paramref name="artifactDirectory"/>.</summary>
    public FileMasterKeySource(string keyPath, string labelsPath, string artifactDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactDirectory);
        key = new CustodyFile("arquivo da chave", Path.GetFullPath(keyPath), CustodyFileFormat.LongestKeyFile);
        labels = new CustodyFile("arquivo dos rótulos", Path.GetFullPath(labelsPath), CustodyFileFormat.LongestLabelsFile);
        this.artifactDirectory = Path.GetFullPath(artifactDirectory);
    }

    /// <summary>Names the two configuration keys that point at the files, not the paths, which are not for the status route to show.</summary>
    public string Origin => "arquivos de custódia, nomeados por MasterKey:File e MasterKey:LabelsFile";

    /// <inheritdoc />
    public async Task<MasterKeyReading> ReadAsync(CancellationToken cancellationToken)
    {
        var absent = new[] { key, labels }.Where(file => !File.Exists(file.Path)).Select(file => $"{file.Name} não encontrado em '{file.Path}'").ToList();
        if (absent.Count > 0)
        {
            return MasterKeyReading.Absent(string.Join("; ", absent));
        }

        try
        {
            var refusal = Refusal(key) ?? Refusal(labels);
            if (refusal is not null)
            {
                return MasterKeyReading.Unreadable(refusal);
            }

            return await ReadFilesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return MasterKeyReading.Unreadable($"arquivo de custódia que não pôde ser aberto ({exception.GetType().Name})");
        }
    }

    private string? Refusal(CustodyFile file)
    {
        if (InsideArtifact(file.Path))
        {
            return $"{file.Name} em '{file.Path}', dentro da pasta do artefato; ele fica fora dela";
        }

        return KeyFilePermission.Excess(file.Path) is { } excess ? $"{file.Name} em '{file.Path}': {excess}" : null;
    }

    private bool InsideArtifact(string path)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var real = new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? path;
        var root = Path.TrimEndingDirectorySeparator(artifactDirectory) + Path.DirectorySeparatorChar;
        return real.StartsWith(root, comparison);
    }

    private async Task<MasterKeyReading> ReadFilesAsync(CancellationToken cancellationToken)
    {
        if (await ContentAsync(labels, cancellationToken).ConfigureAwait(false) is not { } labelsContent)
        {
            return TooLong(labels);
        }

        var (given, labelsProblem) = CustodyFileFormat.ParseLabels(labelsContent);
        if (given is null)
        {
            return MasterKeyReading.Unreadable($"{labels.Name} em '{labels.Path}': {labelsProblem}");
        }

        if (await ContentAsync(key, cancellationToken).ConfigureAwait(false) is not { } keyContent)
        {
            return TooLong(key);
        }

        var (bytes, keyProblem) = CustodyFileFormat.ParseKey(keyContent);
        return bytes is null
            ? MasterKeyReading.Unreadable($"{key.Name} em '{key.Path}': {keyProblem}")
            : MasterKeyReading.FromBytes(bytes, given);
    }

    /// <summary>What the file holds, read through the one handle that also tells its size; null when it is larger than a file of its kind can be.</summary>
    private static async Task<byte[]?> ContentAsync(CustodyFile file, CancellationToken cancellationToken)
    {
        var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            if (stream.Length > file.Longest)
            {
                return null;
            }

            var content = new byte[stream.Length];
            await stream.ReadExactlyAsync(content, cancellationToken).ConfigureAwait(false);
            return content;
        }
    }

    private static MasterKeyReading TooLong(CustodyFile file) =>
        MasterKeyReading.Unreadable($"{file.Name} em '{file.Path}': maior que {file.Longest} bytes, o que um arquivo desse tipo não é");

    private sealed record CustodyFile(string Name, string Path, int Longest);
}
