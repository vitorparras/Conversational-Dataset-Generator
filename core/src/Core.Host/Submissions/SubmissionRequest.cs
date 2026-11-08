using Core.Ingestion;

namespace Core.Host.Submissions;

/// <summary>What the person who submits gives the service: where it goes, in which form and format, under which condition, and the stream of the file with the name it came with.</summary>
internal sealed record SubmissionRequest
{
    /// <summary>The domain the batch belongs to.</summary>
    internal required int DomainId { get; init; }

    /// <summary>The person of the session, who is the author of the opening of the batch.</summary>
    internal required long AuthorId { get; init; }

    /// <summary>The form the material is declared as.</summary>
    internal required MaterialForm DeclaredForm { get; init; }

    /// <summary>The format of the file, as a reader names its own.</summary>
    internal required string SourceFormat { get; init; }

    /// <summary>The condition of origin the material comes under; null when none is declared, and the batch then takes the most restrictive one.</summary>
    internal string? Condition { get; init; }

    /// <summary>Where the material came from, in the words of whoever submits; required unless <see cref="OriginUnknown" /> is set, and kept sealed, never in a log or an export.</summary>
    public string? Origin { get; init; }

    /// <summary>The explicit choice of saying the origin is unknown, which is a valid answer; it excludes <see cref="Origin" />.</summary>
    public bool OriginUnknown { get; init; }

    /// <summary>The stream of the file; the service reads it and never past the limit.</summary>
    internal required Stream Content { get; init; }

    /// <summary>The name the file came with, which decides nothing, is not kept and is not answered.</summary>
    internal string? ReceivedName { get; init; }
}
