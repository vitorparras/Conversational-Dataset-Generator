using Core.Platform.Keys;

namespace Core.Ingestion;

/// <summary>
/// The keyed digests that the criterion compares, one key for the texts and another for the senders, each of its own for the domain: the same text in two domains does not give the same digest,
/// and a text is never comparable with a sender. A digest is not undone without the master key.
/// </summary>
public static class ReplicationDigests
{
    private const string TextPurpose = "replicacao-texto";
    private const string SenderPurpose = "replicacao-remetente";

    /// <summary>The digest of a text for the domain.</summary>
    /// <param name="derivation">The derivation of the master key.</param>
    /// <param name="domainId">The domain.</param>
    /// <param name="text">The text as it arrived.</param>
    public static string OfText(KeyDerivation derivation, int domainId, string text) => Of(derivation, TextPurpose, domainId, text);

    /// <summary>The digest of a sender for the domain.</summary>
    /// <param name="derivation">The derivation of the master key.</param>
    /// <param name="domainId">The domain.</param>
    /// <param name="sender">The sender as the message names it.</param>
    public static string OfSender(KeyDerivation derivation, int domainId, string sender) => Of(derivation, SenderPurpose, domainId, sender);

    /// <summary>The message as the criterion sees it, with the digests of the domain.</summary>
    /// <param name="derivation">The derivation of the master key.</param>
    /// <param name="domainId">The domain.</param>
    /// <param name="place">Where the message is kept.</param>
    /// <param name="measured">The message of a conversation, with the length of its text in code points.</param>
    public static ReplicationOccurrence OccurrenceOf(KeyDerivation derivation, int domainId, ReplicationPlace place, MeasuredMessage measured)
    {
        ArgumentNullException.ThrowIfNull(measured);
        ArgumentOutOfRangeException.ThrowIfNegative(measured.Length);
        var message = measured.Message;
        return new ReplicationOccurrence(place, OfText(derivation, domainId, message.Text), OfSender(derivation, domainId, message.Author), measured.Length, message.Instant);
    }

    private static string Of(KeyDerivation derivation, string purpose, int domainId, string text)
    {
        ArgumentNullException.ThrowIfNull(derivation);
        ArgumentOutOfRangeException.ThrowIfLessThan(domainId, 1);
        return derivation.Digest($"{purpose}/{domainId}", text);
    }
}
