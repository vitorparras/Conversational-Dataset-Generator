using Core.Persistence.People;

namespace Core.Host.Endpoints;

/// <summary>
/// What the one who entered reaches by the access policy (006): the function they hold and the domains granted to them, read once for the request.
/// The route and the menu ask it the same question, so the menu never lists what the route would refuse. The administration of the system and the audit
/// hold no content and no axis of domain, and reach their screens by the function; the other functions reach a screen only with a domain granted.
/// </summary>
internal sealed class ScreenReach
{
    private static readonly IEnumerable<string> WithoutDomain = Enumerable.Repeat(PersonFunctions.Administration, 1).Append(PersonFunctions.Audit);

    private readonly string? function;

    internal ScreenReach(string? function, IEnumerable<string> granted)
    {
        ArgumentNullException.ThrowIfNull(granted);
        this.function = function;
        Granted = granted;
    }

    /// <summary>The reach of no function and no domain, which opens only the pages of everyone.</summary>
    internal static ScreenReach Nothing { get; } = new(function: null, DomainGrants.None);

    /// <summary>The domains granted, which the selector of the header lists.</summary>
    internal IEnumerable<string> Granted { get; }

    /// <summary>How many notices of the person were not read, which the header and the start say.</summary>
    internal int Unread { get; init; }

    /// <summary>Whether the screen of the mark given is reached; a route with no mark is not.</summary>
    internal bool Reaches(ScreenAccess? access)
    {
        if (access?.Functions is null)
        {
            return access is not null;
        }

        return function is not null && access.Functions.Contains(function, StringComparer.Ordinal) && (Granted.Any() || WithoutDomain.Contains(function, StringComparer.Ordinal));
    }
}
