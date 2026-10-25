using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Core.Platform.Logging;

/// <summary>
/// The file destination of the log: every category writes the console's line to the same file. It sits behind the
/// redacting pipeline as the console does, so what it receives is already redacted. Disposing it closes the file.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly LogFile file;
    private readonly ConsoleFormatter formatter;

    private FileLoggerProvider(StreamWriter writer, ConsoleFormatter formatter)
    {
        file = new LogFile(writer);
        this.formatter = formatter;
    }

    /// <summary>
    /// Opens the file for appending, creating it when absent, and writes each event with the formatter given, which
    /// is the console's; null when the path is not one or the file cannot be opened for writing.
    /// </summary>
    public static FileLoggerProvider? Open(string path, ConsoleFormatter formatter)
    {
        try
        {
            return new FileLoggerProvider(
                new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read)), formatter);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, formatter, file.Append);

    /// <inheritdoc />
    public void Dispose() => file.Dispose();
}
