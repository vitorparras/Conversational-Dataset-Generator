namespace Core.Tests.Support.Harness;

/// <summary>One mark of the writing: its half-open span in code points, what was written and its kind.</summary>
/// <param name="Start">The code point offset where the mark starts.</param>
/// <param name="End">The code point offset just past the mark.</param>
/// <param name="Text">What was written.</param>
/// <param name="Kind">The kind of the mark, in the words a failure shows.</param>
public sealed record SignalMark(int Start, int End, string Text, string Kind);
