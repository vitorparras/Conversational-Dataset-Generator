using System.Globalization;

namespace Core.Persistence.Custody.Verification;

/// <summary>
/// How the event of a verification on demand says who asked for it: the reference <c>requested-by/&lt;id&gt;</c>. The event stays written by the system, as the chain requires of the step,
/// and the reference is the one place the asker is kept; a verification the host runs by itself has no reference.
/// </summary>
internal static class RequesterReference
{
    internal const string Prefix = "requested-by/";

    /// <summary>The reference for the asker; null when nobody asked.</summary>
    /// <param name="requestedBy">The id of the asker.</param>
    internal static string? Of(long? requestedBy)
    {
        if (requestedBy is not { } id)
        {
            return null;
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        return Prefix + id.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The asker a reference names; null when it names none.</summary>
    /// <param name="reference">The reference of the event, if it has one.</param>
    internal static long? From(string? reference)
    {
        if (reference is null || !reference.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return long.TryParse(reference.AsSpan(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
    }
}
