using System.Globalization;
using System.Text.Json;

namespace Core.Ingestion.Reading;

/// <summary>The messages of the exported conversation: each one with its id, sender, moment and text, and the one it answers by the id of that one.</summary>
internal sealed class ConversationMessages
{
    private const string IdName = "id";
    private const string SenderName = "remetente";
    private const string MomentName = "instante";
    private const string TextName = "texto";
    private const string AnswersName = "responde_a";

    private readonly Dictionary<string, string> senders = new(StringComparer.Ordinal);
    private readonly List<ReadUnit> read = [];

    private ConversationMessages()
    {
    }

    /// <summary>Reads the array of messages; false when it is not an array, a message is wrong, an id repeats or an answer names an id that is not there.</summary>
    /// <param name="array">The array of the messages.</param>
    /// <param name="units">The units read, in the order of the array.</param>
    internal static bool TryRead(JsonElement array, out IReadOnlyList<ReadUnit> units)
    {
        var messages = new ConversationMessages();
        units = messages.read;
        return array.ValueKind == JsonValueKind.Array && messages.TryAll(array);
    }

    private static bool IsKnown(string name) =>
        name switch
        {
            IdName or SenderName or MomentName => true,
            TextName or AnswersName => true,
            _ => false,
        };

    private bool TryAll(JsonElement array)
    {
        var objects = new List<IDictionary<string, JsonElement>>();
        foreach (var message in array.EnumerateArray())
        {
            var members = JsonMembers.Of(message, IsKnown);
            if (members is null || !TryIdentify(members))
            {
                return false;
            }

            objects.Add(members);
        }

        return objects.TrueForAll(TryUnit);
    }

    private bool TryIdentify(IDictionary<string, JsonElement> members) =>
        JsonMembers.TryText(members, IdName, out var id) && JsonMembers.TryText(members, SenderName, out var sender) && senders.TryAdd(id, sender);

    private bool TryUnit(IDictionary<string, JsonElement> members)
    {
        string? answered = null;
        if (members.ContainsKey(AnswersName) && !TryAnswered(members, out answered))
        {
            return false;
        }

        if (!JsonMembers.TryText(members, SenderName, out var sender) || !JsonMembers.TryText(members, TextName, out var text) || !TryInstant(members, out var instant))
        {
            return false;
        }

        read.Add(new ReadUnit(text, sender, instant, answered, []));
        return true;
    }

    private bool TryAnswered(IDictionary<string, JsonElement> members, out string? answered)
    {
        answered = null;
        return JsonMembers.TryText(members, AnswersName, out var to) && senders.TryGetValue(to, out answered);
    }

    private static bool TryInstant(IDictionary<string, JsonElement> members, out DateTimeOffset instant)
    {
        var parsed = default(DateTimeOffset);
        var held = JsonMembers.TryText(members, MomentName, out var text)
            && Formats().Any(format => DateTimeOffset.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out parsed));
        instant = parsed;
        return held;
    }

    /// <summary>RFC 3339 with the offset always written, as <c>Z</c> or as <c>+hh:mm</c>, with or without the fraction of the second; a moment with no offset is none of them.</summary>
    private static IEnumerable<string> Formats()
    {
        yield return "yyyy-MM-dd'T'HH:mm:ss'Z'";
        yield return "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";
        yield return "yyyy-MM-dd'T'HH:mm:sszzz";
        yield return "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz";
    }
}
