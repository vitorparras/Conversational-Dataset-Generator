using Core.Ingestion;

namespace Core.Host.Submissions;

/// <summary>What the person who submits gives the service: where it goes, in which form and format, under which condition, and the stream of the file with the name it came with.</summary>
internal sealed record SubmissionRequest
{
    /// <summary>The domain the batch belongs to.</summary>
    public required int DomainId { get; init; }

    /// <summary>The person of the session, who is the author of the opening of the batch.</summary>
    public required long AuthorId { get; init; }

    /// <summary>The form the material is declared as.</summary>
    public required MaterialForm DeclaredForm { get; init; }

    /// <summary>The format of the file, as a reader names its own.</summary>
    public required string SourceFormat { get; init; }

    /// <summary>The condition of origin the material comes under; null when none is declared, and the batch then takes the most restrictive one.</summary>
    public string? Condition { get; init; }

    /// <summary>The stream of the file; the service reads it and never past the limit.</summary>
    public required Stream Content { get; init; }

    /// <summary>The name the file came with, which decides nothing, is not kept and is not answered.</summary>
    public string? ReceivedName { get; init; }
}
