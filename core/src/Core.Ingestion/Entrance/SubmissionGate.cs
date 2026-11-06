using System.Security.Cryptography;
using Core.Ingestion.Reading;

namespace Core.Ingestion.Entrance;

/// <summary>
/// The gate of a submission, of the domain alone and writing nothing: it reads the stream up to a limit of bytes and stops at the first byte past it, hashes what it read in the same pass,
/// confirms the format declared by the first bytes of the content, hands the content to the reader of that format, and leaves the name the file came with behind, giving the submission
/// an identifier of its own. The extension of the name enters nowhere.
/// </summary>
public sealed class SubmissionGate
{
    private const int ChunkBytes = 8192;

    private readonly SourceReaders readers;
    private readonly long maxBytes;
    private readonly Func<Guid> newId;

    /// <summary>The gate over the readers of the version and the limit of bytes the configuration gives.</summary>
    /// <param name="readers">The readers, one for each format.</param>
    /// <param name="maxBytes">The most bytes a submission may have; it must be positive.</param>
    public SubmissionGate(SourceReaders readers, long maxBytes)
        : this(readers, maxBytes, Guid.NewGuid)
    {
    }

    /// <summary>The gate with the generator of identifiers given, so that a test can tell which identifier was generated.</summary>
    /// <param name="readers">The readers, one for each format.</param>
    /// <param name="maxBytes">The most bytes a submission may have; it must be positive.</param>
    /// <param name="newId">Generates the identifier of a submission.</param>
    internal SubmissionGate(SourceReaders readers, long maxBytes, Func<Guid> newId)
    {
        ArgumentNullException.ThrowIfNull(readers);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        ArgumentNullException.ThrowIfNull(newId);
        this.readers = readers;
        this.maxBytes = maxBytes;
        this.newId = newId;
    }

    /// <summary>Receives the submission: the material read whole, or the refusal with its reason; the identifier, the digest and the count of bytes come either way.</summary>
    /// <param name="submission">The stream, the format declared and the name received.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<SubmissionResult> ReceiveAsync(Submission submission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(submission.Content);
        ArgumentNullException.ThrowIfNull(submission.Format);
        return ReceivingAsync(submission, cancellationToken);
    }

    private async Task<SubmissionResult> ReceivingAsync(Submission submission, CancellationToken cancellationToken)
    {
        var id = new SubmissionId(newId());
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var content = await TakeAsync(submission.Content, hash, cancellationToken).ConfigureAwait(false);
        var received = (Convert.ToHexStringLower(hash.GetHashAndReset()), content.Length);
        if (content.Length > maxBytes)
        {
            return SubmissionResult.Refused(id, received, SubmissionRefusal.TooLarge);
        }

        return readers.TryGet(submission.Format, out var reader) ? Judge(id, received, reader, content) : SubmissionResult.Refused(id, received, SubmissionRefusal.UnknownFormat);
    }

    private static SubmissionResult Judge(SubmissionId id, (string Digest, long Bytes) received, ISourceReader reader, byte[] content)
    {
        if (!reader.Recognizes(content.AsSpan(0, Math.Min(content.Length, reader.Limits.HeadBytes))))
        {
            return SubmissionResult.Refused(id, received, SubmissionRefusal.FormatNotConfirmed);
        }

        var outcome = reader.Read(content);
        return outcome.Material is { } material
            ? SubmissionResult.Read(id, received, material)
            : SubmissionResult.Refused(id, received, SubmissionRefusal.Unreadable, outcome.Reason);
    }

    /// <summary>Reads up to one byte past the limit and no more, hashing what it reads; a stream that gives more is left unread from the first byte past the limit.</summary>
    private async Task<byte[]> TakeAsync(Stream stream, IncrementalHash hash, CancellationToken cancellationToken)
    {
        var chunk = new byte[ChunkBytes];
        using var taken = new MemoryStream();
        var more = true;
        while (more && taken.Length <= maxBytes)
        {
            var want = (int)Math.Min(chunk.Length, maxBytes + 1 - taken.Length);
            var read = await stream.ReadAsync(chunk.AsMemory(0, want), cancellationToken).ConfigureAwait(false);
            hash.AppendData(chunk.AsSpan(0, read));
            await taken.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            more = read > 0;
        }

        return taken.ToArray();
    }
}
