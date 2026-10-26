using System.Globalization;
using Core.Platform.Versions;

namespace Core.Boundary.Http;

/// <summary>
/// Reads the service, contract and model versions from the version route of the language service on this machine, with the startup secret.
/// It keeps nothing between reads: a service that is down, refuses the secret, takes too long or answers something
/// that is not a version has none of the three, whatever it answered before, and a field it leaves blank is absent by itself.
/// </summary>
/// <param name="client">A client that never goes through a proxy, since the service is on this machine.</param>
/// <param name="settings">The port, the secret and the time limit of the read.</param>
public sealed class HttpServiceVersionReader(HttpClient client, HttpRecognizerSettings settings) : IServiceVersionReader
{
    /// <inheritdoc />
    public async Task<ServiceVersions> ReadAsync(CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(settings.Timeout);
        try
        {
            return await AskAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ServiceVersions.Absent;
        }
        catch (HttpRequestException)
        {
            return ServiceVersions.Absent;
        }
    }

    private static VersionValue Declared(string? version) =>
        string.IsNullOrWhiteSpace(version) ? VersionValue.Absent : VersionValue.Present(version);

    private async Task<ServiceVersions> AskAsync(CancellationToken limit)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{settings.Port}/version")));
        request.Headers.Add(HttpRecognizer.SecretHeader, settings.Secret);
        using var response = await client.SendAsync(request, limit).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return ServiceVersions.Absent;
        }

        var body = await response.Content.ReadAsByteArrayAsync(limit).ConfigureAwait(false);
        return VersionAnswer.Read(body) is { } answer
            ? new ServiceVersions(Declared(answer.ContractVersion), Declared(answer.ServiceVersion), Declared(answer.ModelVersion))
            : ServiceVersions.Absent;
    }
}
