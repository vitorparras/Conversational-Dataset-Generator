// Text fixture, never compiled: an interpolated string in a logging file must be reported.
namespace DirectLogging.Logging;

public static class LineBuilder
{
    public static string Line(string text) => $"line {text}";
}
