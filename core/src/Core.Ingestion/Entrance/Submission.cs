namespace Core.Ingestion.Entrance;

/// <summary>What arrives at the gate: the stream of the content, the format declared for it and the name it came with, which the gate takes only to leave it behind.</summary>
/// <param name="Content">The stream of the content; the gate reads it and never past the limit.</param>
/// <param name="Format">The format declared, as a reader names its own.</param>
/// <param name="ReceivedName">The name the file came with, which decides nothing and crosses nowhere.</param>
public sealed record Submission(Stream Content, string Format, string? ReceivedName);
