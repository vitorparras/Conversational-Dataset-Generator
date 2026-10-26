using System.Security.Cryptography;
using Core.Platform.Startup;

namespace Core.Host.Supervision;

/// <summary>Confirms the language service folder against its manifest before the service starts; an executable the configuration gave skips the folder.</summary>
internal sealed class ServiceFolderCheck : IStartupCheck
{
    private const int HashLength = 64;
    private const int PathStart = HashLength + 2;

    private readonly ServiceFolder folder;
    private readonly string? given;

    internal ServiceFolderCheck(ServiceFolder folder, string? given)
    {
        this.folder = folder;
        this.given = given;
    }

    /// <inheritdoc />
    public string Name => "LanguageServiceFolder";

    /// <inheritdoc />
    public async Task<CheckResult> RunAsync(CancellationToken cancellationToken)
    {
        if (given is not null)
        {
            return CheckResult.Passed(
                Name,
                $"o executável veio de {LanguageServiceConfiguration.ExecutablePath}; a pasta language-service não foi conferida",
                LanguageServiceConfiguration.ExecutablePath);
        }

        if (!File.Exists(folder.Manifest))
        {
            return Refused($"manifesto ausente: {ServiceFolder.ManifestSource}");
        }

        var lines = await LinesAsync(cancellationToken);
        if (lines is null)
        {
            return Refused($"manifesto ilegível: {ServiceFolder.ManifestSource}");
        }

        if (lines.Count == 0)
        {
            return Refused($"manifesto vazio: {ServiceFolder.ManifestSource}");
        }

        for (var number = 0; number < lines.Count; number++)
        {
            var problem = await ProblemAsync(number + 1, lines[number].TrimEnd('\r'), cancellationToken);
            if (problem is not null)
            {
                return Refused(problem);
            }
        }

        return CheckResult.Passed(Name, $"{lines.Count} arquivos conferidos contra {ServiceFolder.ManifestSource}", ServiceFolder.ManifestSource);
    }

    /// <summary>The line in the format of sha256sum: the sum, a space, a space or the mark of binary mode, then the path.</summary>
    private static bool InFormat(string line) =>
        line.Length > PathStart
        && line[..HashLength].All(Uri.IsHexDigit)
        && line[HashLength] == ' '
        && line[HashLength + 1] is ' ' or '*';

    private async Task<List<string>?> LinesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return [.. (await File.ReadAllLinesAsync(folder.Manifest, cancellationToken)).Where(line => line.Length > 0)];
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task<string?> SumOfAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private CheckResult Refused(string reason) => CheckResult.Failed(Name, reason, ServiceFolder.ManifestSource);

    private async Task<string?> ProblemAsync(int number, string line, CancellationToken cancellationToken)
    {
        if (!InFormat(line))
        {
            return $"linha {number} de {ServiceFolder.ManifestSource} fora do formato";
        }

        var relative = line[PathStart..];
        var path = folder.Inside(relative);
        if (path is null)
        {
            return $"linha {number} de {ServiceFolder.ManifestSource} aponta para fora da pasta: {relative}";
        }

        if (!File.Exists(path))
        {
            return $"arquivo ausente: {ServiceFolder.Named(relative)}";
        }

        var sum = await SumOfAsync(path, cancellationToken);
        if (sum is null)
        {
            return $"arquivo ilegível: {ServiceFolder.Named(relative)}";
        }

        return string.Equals(sum, line[..HashLength], StringComparison.OrdinalIgnoreCase)
            ? null
            : $"soma diverge: {ServiceFolder.Named(relative)}";
    }
}
