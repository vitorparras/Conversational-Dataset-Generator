using Core.Platform.Keys;

namespace Core.Host.Submissions;

/// <summary>The key that seals the content could not be read: the path of the file stops before it opens a batch or keeps an item, and the kind of failure is told, never the value found.</summary>
internal sealed class ContentKeyUnavailableException : InvalidOperationException
{
    /// <summary>The exception for a key that was absent, unreadable or of the wrong size.</summary>
    /// <param name="kind">What was wrong with the key.</param>
    internal ContentKeyUnavailableException(MasterKeyFailureKind kind)
        : base("The key that seals the content could not be read; nothing was recorded.")
    {
        Kind = kind;
    }

    /// <summary>What was wrong with the key.</summary>
    internal MasterKeyFailureKind Kind { get; }
}
