namespace Core.Platform.Tests.Logging;

/// <summary>Everything written to the console between construction and dispose, with the real console restored afterwards.</summary>
internal sealed class ConsoleCapture : IDisposable
{
    private static readonly TextWriter Original = Console.Out;
    private readonly StringWriter captured = new();

    public ConsoleCapture()
    {
        Console.SetOut(captured);
    }

    public string Text => captured.ToString();

    public void Dispose()
    {
        Console.SetOut(Original);
        captured.Dispose();
    }
}
