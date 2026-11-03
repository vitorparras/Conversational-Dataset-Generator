namespace Core.Persistence.Recovery;

/// <summary>What a rehearsal counted: the samples of the raw text and of the reversal map that opened, and the ones that did not.</summary>
/// <param name="Raw">How many samples of the raw text opened.</param>
/// <param name="Maps">How many samples of the reversal map opened.</param>
/// <param name="Failed">How many samples of either did not open.</param>
public sealed record SampleCounts(int Raw, int Maps, int Failed)
{
    /// <summary>Whether the rehearsal holds: something opened and nothing failed.</summary>
    public bool Holds => Failed == 0 && Raw + Maps > 0;
}
