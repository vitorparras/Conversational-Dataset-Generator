namespace Core.Ingestion.Entrance;

/// <summary>
/// What the gate gives back for a submission, always with the identifier it generated, the SHA-256 of the bytes it read and how many they were:
/// the material read whole, or the refusal with its reason. Nothing in it is the name the file came with.
/// </summary>
public sealed record SubmissionResult
{
    private SubmissionResult(SubmissionId id, string digest, long bytesRead)
    {
        Id = id;
        Digest = digest;
        BytesRead = bytesRead;
    }

    /// <summary>The identifier the system generated for the submission.</summary>
    public SubmissionId Id { get; }

    /// <summary>The SHA-256 of the bytes read, in lower case hexadecimal; for a content past the limit, of the bytes read before the reading stopped.</summary>
    public string Digest { get; }

    /// <summary>How many bytes were read from the stream, which is never more than the limit and one.</summary>
    public long BytesRead { get; }

    /// <summary>The material, read whole; null when the submission was refused.</summary>
    public ReadMaterial? Material { get; private init; }

    /// <summary>Why the submission was refused; null when it was read.</summary>
    public SubmissionRefusal? Refusal { get; private init; }

    /// <summary>Why the reader could not read, when the refusal is <see cref="SubmissionRefusal.Unreadable" />.</summary>
    public UnreadableReason? Cause { get; private init; }

    /// <summary>The reason in words, which names no word of the content: null when the submission was read.</summary>
    public string? Reason => Refusal switch
    {
        null => null,
        SubmissionRefusal.TooLarge => "o envio passa do limite de tamanho, e a leitura parou no primeiro byte excedente",
        SubmissionRefusal.UnknownFormat => "esta versão não tem leitor para o formato declarado",
        SubmissionRefusal.FormatNotConfirmed => "o conteúdo não confirma o formato declarado",
        SubmissionRefusal.Unreadable => $"a leitura não foi completa ({Ingestion.Refusal.CauseWords(Cause)}), e leitura parcial não é aceita",
        _ => throw new InvalidOperationException("The refusal is not one of the enumeration."),
    };

    internal static SubmissionResult Read(SubmissionId id, (string Digest, long Bytes) received, ReadMaterial material) =>
        new(id, received.Digest, received.Bytes) { Material = material };

    internal static SubmissionResult Refused(SubmissionId id, (string Digest, long Bytes) received, SubmissionRefusal refusal, UnreadableReason? cause = null) =>
        new(id, received.Digest, received.Bytes) { Refusal = refusal, Cause = cause };
}
