namespace Core.Ingestion.Tests.Entrance;

/// <summary>One line of the manifest of the hostile corpus: the file, the name it came with, the outcome expected and, when it differs, the limit of bytes.</summary>
/// <param name="File">The file of the corpus.</param>
/// <param name="Name">The name the file came with, which may be a path or hold control characters.</param>
/// <param name="Expect">The outcome: <c>read</c>, a refusal, or <c>Unreadable:</c> and the cause.</param>
/// <param name="Limit">The limit of bytes, when it is not the usual one.</param>
internal sealed record CorpusEntry(string File, string? Name, string Expect, long? Limit);
