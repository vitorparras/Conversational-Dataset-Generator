using System.Text.Json;

namespace Core.Ingestion.Reading;

/// <summary>
/// The reader of the exported conversation, format <c>cdg-conversa/1</c> (docs/operacao/036): strict JSON, in a fixed depth and size, with no comment, no trailing comma,
/// no member it does not know and no member twice. Whatever fails is unreadable and hands over nothing of what was read; the reason never holds a word of the content.
/// </summary>
public sealed class ExportedConversationReader : ISourceReader
{
    /// <summary>The format this reader reads.</summary>
    internal const string FormatName = "cdg-conversa/1";

    /// <summary>The most bytes the reader takes.</summary>
    internal const int MaxBytes = 16 * 1024 * 1024;

    private const int MaxDepth = 4;
    private const string FormatMember = "formato";
    private const string MessagesMember = "mensagens";
    private static readonly JsonDocumentOptions Strict = new() { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false, MaxDepth = MaxDepth };

    /// <inheritdoc />
    public string Format => FormatName;

    /// <inheritdoc />
    public ReadOutcome Read(ReadOnlyMemory<byte> content)
    {
        if (content.Length > MaxBytes)
        {
            return ReadOutcome.Unreadable(UnreadableReason.Corrupted);
        }

        try
        {
            using var document = JsonDocument.Parse(content, Strict);
            return Interpret(document.RootElement);
        }
        catch (JsonException)
        {
            return ReadOutcome.Unreadable(JsonEnds.EndsBeforeTheContentDoes(content.Span) ? UnreadableReason.Truncated : UnreadableReason.Corrupted);
        }
        catch (InvalidOperationException)
        {
            return ReadOutcome.Unreadable(UnreadableReason.Corrupted);
        }
    }

    private static bool IsKnown(string name) => name is FormatMember or MessagesMember;

    private static ReadOutcome Interpret(JsonElement root)
    {
        var members = JsonMembers.Of(root, IsKnown);
        if (members is null || !JsonMembers.TryText(members, FormatMember, out var declared))
        {
            return ReadOutcome.Unreadable(UnreadableReason.Corrupted);
        }

        if (!string.Equals(declared, FormatName, StringComparison.Ordinal))
        {
            return ReadOutcome.Unreadable(UnreadableReason.UnsupportedFormat);
        }

        return members.TryGetValue(MessagesMember, out var messages) && ConversationMessages.TryRead(messages, out var units)
            ? ReadOutcome.Read(new ReadMaterial(units))
            : ReadOutcome.Unreadable(UnreadableReason.Corrupted);
    }
}
