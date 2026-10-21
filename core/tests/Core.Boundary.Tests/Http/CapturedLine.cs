using Microsoft.Extensions.Logging;

namespace Core.Boundary.Tests.Http;

/// <summary>One line written: its level, its event, the message and the fields.</summary>
internal sealed record CapturedLine(LogLevel Level, int EventId, string Message, IReadOnlyDictionary<string, string?> Fields)
{
    public override string ToString() =>
        $"{Level} {EventId} {Message} {string.Join(' ', Fields.Select(field => $"{field.Key}={field.Value}"))}";
}
