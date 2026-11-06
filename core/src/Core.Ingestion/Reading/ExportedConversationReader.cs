using System.Text;
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
    private const int MaxUnits = 100_000;
    private const int MaxTextBytes = 64 * 1024;
    private const int HeadBytes = 4096;
    private const string FormatMember = "formato";
    private const string MessagesMember = "mensagens";
    private static readonly ReadLimits Declared = new(MaxDepth, MaxUnits, MaxTextBytes, HeadBytes);
    private static readonly JsonDocumentOptions Strict = new() { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false, MaxDepth = MaxDepth };

    /// <inheritdoc />
    public string Format => FormatName;

    /// <inheritdoc />
    public ReadLimits Limits => Declared;

    /// <inheritdoc />
    public bool Recognizes(ReadOnlySpan<byte> head) => ExportedHead.Confirms(head, FormatName);

    /// <inheritdoc />
    public ReadOutcome Read(ReadOnlyMemory<byte> content)
    {
        if (content.Length > MaxBytes)
        {
            return ReadOutcome.Unreadable(UnreadableReason.Corrupted);
        }

        if (JsonNesting.Exceeds(content.Span, MaxDepth))
        {
            return ReadOutcome.Unreadable(UnreadableReason.OverLimits);
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

        if (!members.TryGetValue(MessagesMember, out var messages) || messages.ValueKind != JsonValueKind.Array)
        {
            return ReadOutcome.Unreadable(UnreadableReason.Corrupted);
        }

        if (messages.GetArrayLength() > MaxUnits)
        {
            return ReadOutcome.Unreadable(UnreadableReason.OverLimits);
        }

        if (ExternalReferences.Found(messages))
        {
            return ReadOutcome.Unreadable(UnreadableReason.ExternalReference);
        }

        return ConversationMessages.TryRead(messages, out var units) ? Whole(units) : ReadOutcome.Unreadable(UnreadableReason.Corrupted);
    }

    private static ReadOutcome Whole(IReadOnlyList<ReadUnit> units) =>
        units.Any(unit => Encoding.UTF8.GetByteCount(unit.Text) > MaxTextBytes)
            ? ReadOutcome.Unreadable(UnreadableReason.OverLimits)
            : ReadOutcome.Read(new ReadMaterial(units));
}
