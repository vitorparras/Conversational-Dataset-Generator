using System.Net.Sockets;

namespace Core.Boundary.Http;

/// <summary>Opens the connections of the language service client: none is handed to a request until the other end has proved it holds the secret.</summary>
internal static class ProvenConnection
{
    /// <summary>
    /// Connects, then asks the version route for the proof of a new nonce before anything else is written, so no byte of secret or text
    /// reaches whoever listens on the port without it. A request that carries no secret finds no connection: nothing here is for it.
    /// </summary>
    /// <exception cref="HttpRequestException">The request has no secret, or the other end did not prove it holds it.</exception>
    internal static async ValueTask<Stream> OpenAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        if (!context.InitialRequestMessage.Options.TryGetValue(ServiceRequest.SecretOption, out var secret))
        {
            throw new HttpRequestException(HttpRequestError.ConnectionError, "pedido sem segredo: nada a provar nesta conexão");
        }

        Socket? socket = null;
        try
        {
            socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
            await ProveAsync(socket, context.DnsEndPoint.Port, secret, cancellationToken).ConfigureAwait(false);
            var proven = socket;
            socket = null;
            return new NetworkStream(proven, ownsSocket: true);
        }
        finally
        {
            socket?.Dispose();
        }
    }

    /// <summary>The proof over the socket, which stays open: the stream used here does not own it.</summary>
    private static async Task ProveAsync(Socket socket, int port, string secret, CancellationToken cancellationToken)
    {
        var stream = new NetworkStream(socket, ownsSocket: false);
        await using (stream.ConfigureAwait(false))
        {
            await ServiceProof.DemandAsync(stream, port, secret, cancellationToken).ConfigureAwait(false);
        }
    }
}
