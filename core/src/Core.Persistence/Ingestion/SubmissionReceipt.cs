namespace Core.Persistence.Ingestion;

/// <summary>
/// What a submission brought, kept with the batch it opened: the identifier the system generated, how many bytes came, their SHA-256
/// and the code of a warning, if the file came with a name that had a path.
/// </summary>
/// <param name="Submission">The identifier generated, as 32 lowercase hexadecimal digits.</param>
/// <param name="Bytes">How many bytes were read.</param>
/// <param name="Digest">The SHA-256 of the bytes read, in lowercase hexadecimal.</param>
/// <param name="Warning">The code of the warning, a name of lowercase letters, digits and underscores; never the name of the file.</param>
public sealed record SubmissionReceipt(string Submission, long Bytes, string Digest, string? Warning = null);
