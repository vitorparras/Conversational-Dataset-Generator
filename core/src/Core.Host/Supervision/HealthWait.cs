using System.Globalization;
using Core.Boundary.Http;

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

    /// <summary>
    /// Waits until the service has told its port; false when the process ended first, or when what it wrote is not the
    /// announcement, in which case the wait goes on until the process ends or the limit passes, since there is no port to ask.
    /// </summary>
    private static async Task<bool> KnowsPortAsync(RunningService service, CancellationToken limit)
    {
        var announced = service.PortAnnouncedAsync();
        _ = await Task.WhenAny(announced, service.WaitForExitAsync()).WaitAsync(limit);
        if (announced.IsCompleted && await announced)
        {
            return true;
        }

        await service.WaitForExitAsync().WaitAsync(limit);
        return false;
    }

    private async Task<StartOutcome> PollAsync(RunningService service, string secret, CancellationToken limit)
    {
        if (!await KnowsPortAsync(service, limit))
        {
            return StartOutcome.ProcessExited;
        }

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
        request.AddSecret(secret);
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
