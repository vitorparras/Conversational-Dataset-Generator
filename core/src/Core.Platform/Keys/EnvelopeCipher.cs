using System.Security.Cryptography;
using System.Text;

namespace Core.Platform.Keys;

/// <summary>
/// Seals content in AES-256-GCM under the key of a label and opens it again. The envelope carries its label in clear and the
/// cipher authenticates that header, so an envelope moved to another label, or with its header changed, is refused and not read as empty.
/// The nonce is random, so a key seals no more than about 2^32 envelopes: a rotation moves to a new label before that.
/// </summary>
public sealed class EnvelopeCipher
{
    private readonly KeyDerivation derivation;

    /// <summary>The cipher takes its keys from the derivation.</summary>
    public EnvelopeCipher(KeyDerivation derivation)
    {
        ArgumentNullException.ThrowIfNull(derivation);
        this.derivation = derivation;
    }

    /// <summary>The label an envelope says it was sealed under, which nothing yet proves; it is how a reader knows which key to ask for.</summary>
    public static string LabelOf(ReadOnlySpan<byte> envelope) =>
        EnvelopeLayout.TryRead(envelope, out var layout, out var reason) ? layout.Label : throw new EnvelopeRefusedException(reason);

    /// <summary>The content sealed under the label, with a nonce drawn for this seal alone; the same content sealed twice gives two envelopes.</summary>
    public byte[] Seal(string label, ReadOnlySpan<byte> content)
    {
        var labelBytes = Encoding.UTF8.GetBytes(label);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(labelBytes.Length, ushort.MaxValue);
        var envelope = new byte[EnvelopeLayout.SizeFor(labelBytes.Length, content.Length)];
        EnvelopeLayout.WriteHeader(envelope, labelBytes);
        var headerLength = envelope.Length - EnvelopeLayout.NonceLength - content.Length - EnvelopeLayout.TagLength;
        var nonce = envelope.AsSpan(headerLength, EnvelopeLayout.NonceLength);
        RandomNumberGenerator.Fill(nonce);
        using var key = derivation.Derive(label);
        using var aes = new AesGcm(key.AsSpan(), EnvelopeLayout.TagLength);
        aes.Encrypt(nonce, content, envelope.AsSpan(headerLength + EnvelopeLayout.NonceLength, content.Length), envelope.AsSpan(^EnvelopeLayout.TagLength..), envelope.AsSpan(..headerLength));
        return envelope;
    }

    /// <summary>The content of an envelope sealed under the label; any other envelope is refused with <see cref="EnvelopeRefusedException"/>.</summary>
    public byte[] Open(string label, ReadOnlySpan<byte> envelope)
    {
        if (!EnvelopeLayout.TryRead(envelope, out var layout, out var reason))
        {
            throw new EnvelopeRefusedException(reason);
        }

        if (!string.Equals(layout.Label, label, StringComparison.Ordinal))
        {
            throw new EnvelopeRefusedException($"The label in the header is not '{label}'.");
        }

        return Decrypt(label, layout);
    }

    private byte[] Decrypt(string label, EnvelopeLayout layout)
    {
        var content = new byte[layout.Content.Length];
        using var key = derivation.Derive(label);
        using var aes = new AesGcm(key.AsSpan(), EnvelopeLayout.TagLength);
        try
        {
            aes.Decrypt(layout.Nonce, layout.Content, layout.Tag, content, layout.Header);
        }
        catch (AuthenticationTagMismatchException mismatch)
        {
            throw new EnvelopeRefusedException("The envelope did not authenticate under the key of its label.", mismatch);
        }

        return content;
    }
}
