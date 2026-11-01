using System.Security.Cryptography.X509Certificates;

namespace Core.Platform.Tls;

/// <summary>What the source gave: the certificate with its private key, or every reason there is none; the owner releases it.</summary>
public sealed class TlsMaterialReading : IDisposable
{
    private TlsMaterialReading()
    {
    }

    /// <summary>The certificate with its private key; null when anything was refused.</summary>
    public X509Certificate2? Certificate { get; private init; }

    /// <summary>Everything that was refused, in words for the operator, each naming the file and what was found, never a value of the key; empty when the material is ready.</summary>
    public IReadOnlyList<string> Problems { get; private init; } = [];

    /// <summary>The certificate with its key, which the reading now owns.</summary>
    internal static TlsMaterialReading Ready(X509Certificate2 certificate) => new() { Certificate = certificate };

    internal static TlsMaterialReading Refused(IReadOnlyList<string> problems)
    {
        if (problems.Count == 0)
        {
            throw new ArgumentException("A refusal needs at least one problem.", nameof(problems));
        }

        return new TlsMaterialReading { Problems = problems };
    }

    /// <summary>Releases the certificate and its key, if any.</summary>
    public void Dispose() => Certificate?.Dispose();

    /// <summary>Whether the material is ready, and nothing else: a log line or an exception that carries the reading never carries the key.</summary>
    public override string ToString() => Certificate is null ? $"TLS material refused ({Problems.Count} problems)" : "TLS material ready";
}
