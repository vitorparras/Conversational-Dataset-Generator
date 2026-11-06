using System.Globalization;
using System.Text.Json;

namespace Core.Ingestion.Reading;

/// <summary>
/// The messages of the exported conversation: each one with its id, sender, moment and text, and the one it answers by the id of that one,
/// given as the position of that one among the messages.
/// </summary>
internal sealed class ConversationMessages
{
    private const string IdName = "id";
    private const string SenderName = "remetente";
    private const string MomentName = "instante";
    private const string TextName = "texto";
    private const string AnswersName = "responde_a";

    private readonly Dictionary<string, int> positions = new(StringComparer.Ordinal);
    private readonly List<ReadUnit> read;

    private ConversationMessages(List<ReadUnit> read)
    {
        this.read = read;
    }

    /// <summary>Reads the array of messages; false when it is not an array, a message is wrong, an id repeats or an answer names an id that is not there or its own.</summary>
    /// <param name="array">The array of the messages.</param>
    /// <param name="units">The units read, in the order of the array, each answer being the position of the message answered.</param>
    internal static bool TryRead(JsonElement array, out IReadOnlyList<ReadUnit> units)
    {
        var read = new List<ReadUnit>();
        var messages = new ConversationMessages(read);
        units = read;
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
            if (members is null || !JsonMembers.TryText(members, IdName, out var id) || !positions.TryAdd(id, objects.Count))
            {
                return false;
            }

            objects.Add(members);
        }

        return Enumerable.Range(0, objects.Count).All(position => TryUnit(objects[position], position));
    }

    private bool TryUnit(IDictionary<string, JsonElement> members, int position)
    {
        int? answered = null;
        if (members.ContainsKey(AnswersName) && !TryAnswered(members, position, out answered))
        {
            return false;
        }

        if (!JsonMembers.TryText(members, SenderName, out var sender) || !JsonMembers.TryText(members, TextName, out var text) || !TryInstant(members, out var instant))
        {
            return false;
        }

        var none = new List<Passage>();
        read.Add(new ReadUnit(text, sender, instant, answered, none));
        return true;
    }

    private bool TryAnswered(IDictionary<string, JsonElement> members, int own, out int? answered)
    {
        answered = null;
        if (!JsonMembers.TryText(members, AnswersName, out var to) || !positions.TryGetValue(to, out var position) || position == own)
        {
            return false;
        }

        answered = position;
        return true;
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
