using System.Security.Authentication;
using System.Text;

namespace Core.Boundary.Http;

/// <summary>
/// Sends the seal of the chain, in its text, to the destination the installation declared, over https with the validation of the certificate that the platform does by default and no way
/// round it. The time it waits is limited, a redirect is not followed, and the answer that is not a success is a failure: only a seal that arrived may be recorded.
/// </summary>
public sealed class SealDelivery : IDisposable
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ConnectLimit = TimeSpan.FromSeconds(10);

    private readonly HttpClient client;
    private readonly Uri destination;

    /// <summary>The delivery the host uses: to an https address, through the handler of <see cref="Handler" />.</summary>
    /// <param name="destination">An https address.</param>
    public SealDelivery(Uri destination)
        : this(destination, Handler())
    {
    }

    /// <summary>The delivery over the handler given, which a test replaces and the host does not.</summary>
    /// <param name="destination">An https address.</param>
    /// <param name="handler">The handler the requests go through.</param>
    internal SealDelivery(Uri destination, HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(handler);
        if (destination.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("the seal leaves only over https", nameof(destination));
        }

        this.destination = destination;
        client = new HttpClient(handler) { Timeout = Limit };
    }

    /// <summary>The handler the host sends with: no redirect is followed, the connection has a time limit shorter than the request's and the certificate is judged by the default validation.</summary>
    internal static SocketsHttpHandler Handler() => new() { AllowAutoRedirect = false, ConnectTimeout = ConnectLimit };

    /// <summary>Sends the text of a seal.</summary>
    /// <param name="text">The text of the seal.</param>
    /// <param name="cancellationToken">Cancels the sending.</param>
    public async Task<SealDeliveryResult> SendAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        try
        {
            using var content = new StringContent(text, Encoding.UTF8, "text/plain");
            using var response = await client.PostAsync(destination, content, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? SealDeliveryResult.Arrived : SealDeliveryResult.Failed(SealDeliveryFailure.DestinationAnswered, (int)response.StatusCode);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SealDeliveryResult.Failed(SealDeliveryFailure.TimedOut);
        }
        catch (HttpRequestException failure)
        {
            return SealDeliveryResult.Failed(FailureOf(failure));
        }
    }

    private static SealDeliveryFailure FailureOf(HttpRequestException failure) => failure.InnerException switch
    {
        AuthenticationException => SealDeliveryFailure.CertificateRejected,
        TimeoutException => SealDeliveryFailure.TimedOut,
        _ => SealDeliveryFailure.Unreachable,
    };

    /// <inheritdoc />
    public void Dispose() => client.Dispose();
}
