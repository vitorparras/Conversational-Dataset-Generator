using Core.Platform.Logging;

namespace Core.Boundary.Http;

/// <summary>Where the language service listens on this machine, the secret it was started with, and how long one recognition may take.</summary>
public sealed class HttpRecognizerSettings
{
    /// <summary>The settings of one running service; a service started again has another port and another secret.</summary>
    /// <param name="port">The loopback port the service listens on.</param>
    /// <param name="secret">The secret the service was started with, sent in a header on every request.</param>
    /// <param name="timeout">How long one recognition may take, the version read included.</param>
    /// <exception cref="ArgumentException">The secret is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The time limit is not positive, which would leave a recognition waiting forever, or is longer than <see cref="LongestTimeout"/>.
    /// </exception>
    public HttpRecognizerSettings(int port, string secret, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(timeout, LongestTimeout);
        Port = port;
        Secret = secret;
        Timeout = timeout;
    }

    /// <summary>The longest time one recognition may take: an hour, well inside what a cancellation timer accepts.</summary>
    public static TimeSpan LongestTimeout { get; } = TimeSpan.FromHours(1);

    /// <summary>The loopback port the service listens on.</summary>
    public int Port { get; }

    /// <summary>The secret the service was started with.</summary>
    [Secret]
    public string Secret { get; }

    /// <summary>How long one recognition may take.</summary>
    public TimeSpan Timeout { get; }
}
