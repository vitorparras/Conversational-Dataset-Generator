using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using Core.Boundary.Contracts;
using Microsoft.Extensions.Logging;

namespace Core.Boundary.Http;

/// <summary>
/// Asks the language service on this machine for the occurrences in a text, or in a whole item, with the startup secret on every request.
/// The contract version is read from the service on every call, so the class keeps no state; the texts are sent only to
/// a service that speaks the version of the request, and the answer is used only when the validator accepts it whole.
/// </summary>
/// <param name="client">A client that never goes through a proxy, since the service is on this machine.</param>
/// <param name="settings">The port, the secret and the time limit of the running service.</param>
/// <param name="logger">Where the outcome of each request is written, by its reference only.</param>
public sealed class HttpRecognizer(HttpClient client, HttpRecognizerSettings settings, ILogger<HttpRecognizer> logger) : IRecognizer
{
    /// <summary>A body that is not the answer the contract declares, whatever the reason, is never used.</summary>
    private const UnavailabilityReason NotTheContract = UnavailabilityReason.MalformedResponse;

    /// <inheritdoc />
    /// <exception cref="OperationCanceledException">The caller cancelled; a time limit that passes is an outcome instead.</exception>
    public Task<RecognitionOutcome> RecognizeAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RecognizeTextAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="OperationCanceledException">The caller cancelled; a time limit that passes is an outcome instead.</exception>
    public Task<ItemRecognitionOutcome> RecognizeItemAsync(ItemAnalysisRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RecognizeWholeItemAsync(request, cancellationToken);
    }

    /// <summary>Only a connection that was never made is a service out of reach; a connection cut mid-answer, or an answer that is not HTTP, is not the contract.</summary>
    private static UnavailabilityReason Unreached(HttpRequestException failure) =>
        failure.HttpRequestError is HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError
            ? UnavailabilityReason.ConnectionRefused
            : NotTheContract;

    private static long Elapsed(long started) => (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    private async Task<RecognitionOutcome> RecognizeTextAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        var call = new ServiceCall(request.Reference, request.ContractVersion, "analyze", request);
        var exchange = await ExchangeWithinLimitAsync(call, cancellationToken).ConfigureAwait(false);
        if (exchange.Reason is { } unreached)
        {
            return RecognitionOutcome.Unavailable(unreached);
        }

        var validation = ResponseValidator.Validate(request, exchange.Declared!, exchange.Body);
        if (Refusal(request.Reference, validation, exchange.Started) is { } refused)
        {
            return RecognitionOutcome.Unavailable(refused);
        }

        var response = validation.Response!;
        var summary = new AnalysisSummary(request.Reference, response.Occurrences.Count, response.ModelVersion, Elapsed(exchange.Started));
        logger.Analyzed(summary);
        return RecognitionOutcome.Analyzed(response);
    }

    private async Task<ItemRecognitionOutcome> RecognizeWholeItemAsync(ItemAnalysisRequest request, CancellationToken cancellationToken)
    {
        var call = new ServiceCall(request.Reference, request.ContractVersion, "analyze-item", request);
        var exchange = await ExchangeWithinLimitAsync(call, cancellationToken).ConfigureAwait(false);
        if (exchange.Reason is { } unreached)
        {
            return ItemRecognitionOutcome.Unavailable(unreached);
        }

        var validation = ResponseValidator.ValidateItem(request, exchange.Declared!, exchange.Body);
        if (Refusal(request.Reference, validation, exchange.Started) is { } refused)
        {
            return ItemRecognitionOutcome.Unavailable(refused);
        }

        var response = validation.Response!;
        var summary = new AnalysisSummary(request.Reference, response.Occurrences.Count, response.ModelVersion, Elapsed(exchange.Started));
        logger.Analyzed(summary);
        return ItemRecognitionOutcome.Analyzed(response);
    }

    private async Task<Exchange> ExchangeWithinLimitAsync(ServiceCall call, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(settings.Timeout);
        try
        {
            return await ExchangeAsync(call, started, limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable(call.Reference, UnavailabilityReason.TimedOut, started);
        }
        catch (HttpRequestException failure)
        {
            return Unavailable(call.Reference, Unreached(failure), started);
        }
    }

    private async Task<Exchange> ExchangeAsync(ServiceCall call, long started, CancellationToken limit)
    {
        using var version = new HttpRequestMessage(HttpMethod.Get, Route("version"));
        var versionBody = await ReceiveAsync(version, call.Reference, started, limit).ConfigureAwait(false);
        if (versionBody is null)
        {
            return Exchange.Failed(NotTheContract, started);
        }

        var declared = VersionAnswer.ContractVersionIn(versionBody);
        if (declared is null)
        {
            return Unavailable(call.Reference, NotTheContract, started);
        }

        if (!string.Equals(declared, call.ContractVersion, StringComparison.Ordinal))
        {
            return Unavailable(call.Reference, UnavailabilityReason.UnknownContractVersion, started);
        }

        using var analyze = new HttpRequestMessage(HttpMethod.Post, Route(call.Path));
        analyze.Content = JsonContent.Create(call.Request, call.Request.GetType(), options: ContractJson.Options);
        var body = await ReceiveAsync(analyze, call.Reference, started, limit).ConfigureAwait(false);
        return body is null ? Exchange.Failed(NotTheContract, started) : new Exchange(declared, body, null, started);
    }

    /// <summary>The body of an answer with a success status; null, with the status written, when the service refused the request.</summary>
    private async Task<byte[]?> ReceiveAsync(HttpRequestMessage message, string reference, long started, CancellationToken limit)
    {
        message.AddSecret(settings.Secret);
        using var response = await client.SendAsync(message, limit).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsByteArrayAsync(limit).ConfigureAwait(false);
        }

        var elapsed = Elapsed(started);
        logger.StatusRefused(reference, (int)response.StatusCode, elapsed);
        return null;
    }

    /// <summary>The reason to give for an answer the validator refused, written with the problem; null when it was accepted.</summary>
    private UnavailabilityReason? Refusal<TResponse>(string reference, ResponseValidation<TResponse> validation, long started)
        where TResponse : class
    {
        if (validation.Problem is not { } problem)
        {
            return null;
        }

        var elapsed = Elapsed(started);
        logger.AnswerRefused(reference, problem, elapsed);
        return problem == ValidationProblem.UnknownContractVersion ? UnavailabilityReason.UnknownContractVersion : NotTheContract;
    }

    private Exchange Unavailable(string reference, UnavailabilityReason reason, long started)
    {
        var elapsed = Elapsed(started);
        logger.Unavailable(reference, reason, elapsed);
        return Exchange.Failed(reason, started);
    }

    private Uri Route(string path) => new(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{settings.Port}/{path}"));

    /// <summary>One request to the service: whose reference, which contract, which route and what is sent.</summary>
    private sealed record ServiceCall(string Reference, string ContractVersion, string Path, object Request);

    /// <summary>The declared version and the body of an answer with a success status, or the reason there is none.</summary>
    private sealed record Exchange(string? Declared, byte[] Body, UnavailabilityReason? Reason, long Started)
    {
        internal static Exchange Failed(UnavailabilityReason reason, long started) => new(null, [], reason, started);
    }
}
