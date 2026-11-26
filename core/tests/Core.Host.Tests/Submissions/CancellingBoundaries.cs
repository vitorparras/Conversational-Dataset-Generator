using Core.Boundary.Contracts;
using Core.Boundary.Text;

namespace Core.Host.Tests.Submissions;

/// <summary>The boundaries of a text that cancel the submission the first time a text is measured, by the marking or by the swap, and otherwise answer as the real ones.</summary>
internal sealed class CancellingBoundaries : ITextBoundaries
{
    private readonly TextBoundaries real = new();
    private readonly CancellationTokenSource cancelling;

    internal CancellingBoundaries(CancellationTokenSource cancelling)
    {
        this.cancelling = cancelling;
    }

    public bool SplitsPair(string text, int utf16Index) => real.SplitsPair(text, utf16Index);

    public string Slice(string text, Occurrence occurrence) => real.Slice(text, occurrence);

    public string Span(string text, int start, int until) => real.Span(text, start, until);

    public IReadOnlyList<string> CodePoints(string text) => real.CodePoints(text);

    public int CodePointCount(string text)
    {
        cancelling.Cancel();
        return real.CodePointCount(text);
    }
}
