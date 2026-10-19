using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using Core.Boundary.Contracts;
using Microsoft.Extensions.Logging;

namespace Core.Boundary.Http;

/// <summary>
/// Asks the language service on this machine for the occurrences in a text, with the startup secret on every request.
/// The contract version is read from the service on every call, so the class keeps no state; the text is sent only to
/// a service that speaks the version of the request, and the answer is used only when the validator accepts it whole.
/// </summary>
/// <param name="client">A client that never goes through a proxy, since the service is on this machine.</param>
/// <param name="settings">The port, the secret and the time limit of the running service.</param>
/// <param name="logger">Where the outcome of each request is written, by its reference only.</param>
public sealed class HttpRecognizer(HttpClient client, HttpRecognizerSettings settings, ILogger<HttpRecognizer> logger) : IRecognizer
{
    private const string SecretHeader = "x-service-secret";

    /// <summary>A body that is not the answer the contract declares, whatever the reason, is never used.</summary>
    private const UnavailabilityReason NotTheContract = UnavailabilityReason.MalformedResponse;

    /// <inheritdoc />
    /// <exception cref="OperationCanceledException">The caller cancelled; a time limit that passes is an outcome instead.</exception>
    public Task<RecognitionOutcome> RecognizeAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RecognizeWithinLimitAsync(request, cancellationToken);
    }

    /// <summary>Only a connection that was never made is a service out of reach; a connection cut mid-answer, or an answer that is not HTTP, is not the contract.</summary>
    private static UnavailabilityReason Unreached(HttpRequestException failure) =>
        failure.HttpRequestError is HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError
            ? UnavailabilityReason.ConnectionRefused
            : NotTheContract;

    private static long Elapsed(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    private async Task<RecognitionOutcome> RecognizeWithinLimitAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(settings.Timeout);
        try
        {
            return await AskAsync(request, started, limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable(request.Reference, UnavailabilityReason.TimedOut, started);
        }
        catch (HttpRequestException failure)
        {
            return Unavailable(request.Reference, Unreached(failure), started);
        }
    }

    private async Task<RecognitionOutcome> AskAsync(AnalysisRequest request, long started, CancellationToken limit)
    {
        using var version = new HttpRequestMessage(HttpMethod.Get, Route("version"));
        var versionBody = await ReceiveAsync(version, request.Reference, started, limit).ConfigureAwait(false);
        if (versionBody is null)
        {
            return RecognitionOutcome.Unavailable(NotTheContract);
        }

        var declared = VersionAnswer.ContractVersionIn(versionBody);
        if (declared is null)
        {
            return Unavailable(request.Reference, NotTheContract, started);
        }

        if (!string.Equals(declared, request.ContractVersion, StringComparison.Ordinal))
        {
            return Unavailable(request.Reference, UnavailabilityReason.UnknownContractVersion, started);
        }

        using var analyze = new HttpRequestMessage(HttpMethod.Post, Route("analyze"));
        analyze.Content = JsonContent.Create(request, options: ContractJson.Options);
        var body = await ReceiveAsync(analyze, request.Reference, started, limit).ConfigureAwait(false);
        return body is null
            ? RecognitionOutcome.Unavailable(NotTheContract)
            : Judge(request, ResponseValidator.Validate(request, declared, body), started);
    }

    /// <summary>The body of an answer with a success status; null, with the status written, when the service refused the request.</summary>
    private async Task<byte[]?> ReceiveAsync(HttpRequestMessage message, string reference, long started, CancellationToken limit)
    {
        message.Headers.Add(SecretHeader, settings.Secret);
        using var response = await client.SendAsync(message, limit).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsByteArrayAsync(limit).ConfigureAwait(false);
        }

        var elapsed = Elapsed(started);
        logger.StatusRefused(reference, (int)response.StatusCode, elapsed);
        return null;
    }

    private RecognitionOutcome Judge(AnalysisRequest request, ResponseValidation validation, long started)
    {
        var elapsed = Elapsed(started);
        if (validation.Problem is { } problem)
        {
            logger.AnswerRefused(request.Reference, problem, elapsed);
            return RecognitionOutcome.Unavailable(
                problem == ValidationProblem.UnknownContractVersion ? UnavailabilityReason.UnknownContractVersion : NotTheContract);
        }

        var response = validation.Response!;
        var summary = new AnalysisSummary(request.Reference, response.Occurrences.Count, response.ModelVersion, elapsed);
        logger.Analyzed(summary);
        return RecognitionOutcome.Analyzed(response);
    }

    private RecognitionOutcome Unavailable(string reference, UnavailabilityReason reason, long started)
    {
        var elapsed = Elapsed(started);
        logger.Unavailable(reference, reason, elapsed);
        return RecognitionOutcome.Unavailable(reason);
    }

    private Uri Route(string path) => new(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{settings.Port}/{path}"));
}
