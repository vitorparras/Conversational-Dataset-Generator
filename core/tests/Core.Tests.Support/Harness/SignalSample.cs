namespace Core.Tests.Support.Harness;

/// <summary>A generated text and the marks of the writing it carries, in order.</summary>
/// <param name="Text">The text.</param>
/// <param name="Marks">Each mark, with its span in code points.</param>
public sealed record SignalSample(string Text, IReadOnlyList<SignalMark> Marks);
