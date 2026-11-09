using System.Text;

namespace Core.Host.Tests.Logging;

/// <summary>
/// Everything written to the console between construction and dispose, with the real console restored afterwards.
/// The writer that stood in for the console stays writable after the dispose: anything that took <see cref="Console.Out"/> in that window and kept it,
/// such as the logger of Testcontainers when the shared database starts at the same time, writes into it later instead of failing the run.
/// </summary>
internal sealed class ConsoleCapture : IDisposable
{
    private static readonly TextWriter Original = Console.Out;
    private readonly LeftOpen captured = new();

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

    /// <summary>A text writer over a buffer that a dispose does not close, since another holder may still write to it.</summary>
    private sealed class LeftOpen : TextWriter
    {
        private readonly StringBuilder text = new();

        public override Encoding Encoding => Encoding.Unicode;

        public override void Write(char value) => text.Append(value);

        public override void Write(string? value) => text.Append(value);

        public override string ToString() => text.ToString();
    }
}
