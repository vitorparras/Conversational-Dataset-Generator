using System.Globalization;

namespace Core.Host.Supervision;

/// <summary>Waits for the service to answer its authenticated health route.</summary>
internal sealed class HealthWait
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly HttpClient client;

    internal HealthWait(HttpClient client)
    {
        this.client = client;
    }

    internal async Task<StartOutcome> UntilHealthyAsync(
        RunningService service, string secret, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            return await PollAsync(service, secret, limit.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StartOutcome.TimedOut;
        }
    }

    private async Task<StartOutcome> PollAsync(RunningService service, string secret, CancellationToken limit)
    {
        while (!service.HasExited)
        {
            if (await AnswersAsync(service.Port, secret, limit))
            {
                return StartOutcome.Started;
            }

            await Task.Delay(PollInterval, limit);
        }

        return StartOutcome.ProcessExited;
    }

    private async Task<bool> AnswersAsync(int port, string secret, CancellationToken limit)
    {
        var address = new Uri(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/health"));
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.Add("x-service-secret", secret);
        try
        {
            using var response = await client.SendAsync(request, limit);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
