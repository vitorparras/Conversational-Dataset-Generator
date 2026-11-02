using System.Globalization;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>A folder under the temporary path that stands for the folder where the server writes its csvlog, with lines written the way the server writes them; removed with what is in it.</summary>
internal sealed class ServerLogFolder : IDisposable
{
    internal ServerLogFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cdg-serverlog-test-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(Path);
    }

    internal string Path { get; }

    /// <summary>One line of the csvlog for a statement pgaudit audited; the statement is quoted by pgaudit when it holds a comma or a quote, and the whole message by the csv.</summary>
    internal static string AuditLine(DateTimeOffset at, string role, string command, string? objectName, string statement = "SELECT 1")
    {
        var kind = objectName is null ? string.Empty : "TABLE";
        var message = $"AUDIT: SESSION,1,1,WRITE,{command},{kind},{objectName},{Quoted(statement)},<not logged>";
        var t = at.UtcDateTime;
        var time = string.Create(CultureInfo.InvariantCulture, $"{t.Year:D4}-{t.Month:D2}-{t.Day:D2} {t.Hour:D2}:{t.Minute:D2}:{t.Second:D2}.{t.Millisecond:D3}");
        return $"{time} UTC,\"{role}\",\"cdg\",90,\"127.0.0.1:49204\",6ae775a3.5a,1,\"{command}\",{time} UTC,3/2,750,LOG,00000,{Quoted(message)},,,,,,,,,\"psql\",\"client backend\",,0";
    }

    internal static string Quoted(string text) =>
        text.Contains(',', StringComparison.Ordinal) || text.Contains('"', StringComparison.Ordinal) || text.Contains('\n', StringComparison.Ordinal)
            ? $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : text;

    /// <summary>Writes a log file of the lines and gives it the time of its last write.</summary>
    internal string Write(string name, DateTimeOffset lastWrite, params string[] lines)
    {
        var file = System.IO.Path.Combine(Path, name);
        File.WriteAllText(file, string.Join('\n', lines) + "\n");
        File.SetLastWriteTimeUtc(file, lastWrite.UtcDateTime);
        return file;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
