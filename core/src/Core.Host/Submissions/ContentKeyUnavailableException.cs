using Core.Platform.Keys;

namespace Core.Host.Submissions;

/// <summary>
/// The key that seals the content could not be read: no content is kept, and the service that comes with it has no key and only registers the submission as turned back.
/// </summary>
internal sealed class ContentKeyUnavailableException : InvalidOperationException
{
    internal ContentKeyUnavailableException(MasterKeyFailureKind kind, SubmissionService unsealed)
        : base("The key that seals the content could not be read; no content was kept.")
    {
        Kind = kind;
        Unsealed = unsealed;
    }

    /// <summary>What was wrong with the key.</summary>
    internal MasterKeyFailureKind Kind { get; }

    /// <summary>The service with no key to seal with: it registers the submission as rejected, without item and without any of the material.</summary>
    internal SubmissionService Unsealed { get; }
}
