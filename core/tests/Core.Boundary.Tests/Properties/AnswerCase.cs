using Core.Boundary.Contracts;

namespace Core.Tests.Properties.Boundary;

/// <summary>A generated text and the occurrences an answer about it carries, in code points, as the language service would send them.</summary>
internal sealed record AnswerCase(string Text, IReadOnlyList<Occurrence> Occurrences);
