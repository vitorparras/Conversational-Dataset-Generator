using System.Globalization;
using Core.Platform.Versions;

namespace Core.Boundary.Http;

/// <summary>
/// Reads the model version from the version route of the language service on this machine, with the startup secret.
/// It keeps nothing between reads: a service that is down, refuses the secret, takes too long or answers something
/// that is not a version, or declares no model, has no model version, whatever it answered before.
/// </summary>
/// <param name="client">A client that never goes through a proxy, since the service is on this machine.</param>
/// <param name="settings">The port, the secret and the time limit of the read.</param>
public sealed class HttpModelVersionReader(HttpClient client, HttpRecognizerSettings settings) : IModelVersionReader
{
    /// <inheritdoc />
    public async Task<VersionValue> ReadAsync(CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(settings.Timeout);
        try
        {
            return await AskAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return VersionValue.Absent;
        }
        catch (HttpRequestException)
        {
            return VersionValue.Absent;
        }
    }

    private async Task<VersionValue> AskAsync(CancellationToken limit)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, new Uri(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{settings.Port}/version")));
        request.Headers.Add(HttpRecognizer.SecretHeader, settings.Secret);
        using var response = await client.SendAsync(request, limit).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return VersionValue.Absent;
        }

        var body = await response.Content.ReadAsByteArrayAsync(limit).ConfigureAwait(false);
        return VersionAnswer.ModelVersionIn(body) is { } model && !string.IsNullOrWhiteSpace(model)
            ? VersionValue.Present(model)
            : VersionValue.Absent;
    }
}
