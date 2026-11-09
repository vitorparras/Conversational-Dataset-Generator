namespace Core.Persistence.Ingestion;

/// <summary>The condition of a set of items that bring different ones: the most restrictive among them, whatever the proportion, so that no regime depends on which one is the majority.</summary>
public static class EffectiveCondition
{
    /// <summary>The most restrictive of the conditions present; the order and the count of the items do not change it, and it is always one of them.</summary>
    /// <param name="order">The conditions the installation knows, the most restrictive first.</param>
    /// <param name="present">The condition of each item of the set; it must not be empty, and each must be one the installation knows.</param>
    public static string Of(OriginConditions order, IEnumerable<string> present)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(present);
        var strictest = int.MaxValue;
        foreach (var name in present)
        {
            var rank = order.RankOf(name);
            strictest = rank < 0 ? throw new ArgumentException("A condition the installation does not know.", nameof(present)) : Math.Min(strictest, rank);
        }

        return strictest == int.MaxValue ? throw new ArgumentException("A set with no item has no condition.", nameof(present)) : order.Names[strictest];
    }
}
