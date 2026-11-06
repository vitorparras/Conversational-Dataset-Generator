using System.Text.Json;

namespace Core.Ingestion.Reading;

/// <summary>The members of a JSON object, taken only when it holds no member twice and none that is not known.</summary>
internal static class JsonMembers
{
    /// <summary>The members by name; null when the element is not an object, holds a name twice or holds a name that is not known.</summary>
    /// <param name="element">The element to take the members of.</param>
    /// <param name="known">Tells whether an object may hold the name.</param>
    internal static Dictionary<string, JsonElement>? Of(JsonElement element, Predicate<string> known)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var names = element.EnumerateObject().Select(member => member.Name).ToList();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Count || !names.TrueForAll(known))
        {
            return null;
        }

        return element.EnumerateObject().ToDictionary(member => member.Name, member => member.Value, StringComparer.Ordinal);
    }

    /// <summary>The text of a member that is a string; false for a member that is absent or is anything else.</summary>
    /// <param name="members">The members of the object.</param>
    /// <param name="name">The name of the member.</param>
    /// <param name="text">The text of the member.</param>
    internal static bool TryText(IDictionary<string, JsonElement> members, string name, out string text)
    {
        text = string.Empty;
        if (!members.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        text = value.GetString()!;
        return true;
    }
}
