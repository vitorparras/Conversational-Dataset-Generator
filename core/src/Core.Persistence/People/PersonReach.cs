namespace Core.Persistence.People;

/// <summary>What a person reaches: the domains where a grant lets them in. Nobody reaches anything by omission, so no grant is an empty list and never an error.</summary>
/// <param name="DomainIds">The identifiers of the domains granted.</param>
public sealed record PersonReach(IReadOnlyList<int> DomainIds)
{
    /// <summary>The reach of whoever holds no grant.</summary>
    public static PersonReach None { get; } = new([]);
}
