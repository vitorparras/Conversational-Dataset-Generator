using Core.Boundary.Contracts;
using Core.Ingestion.Deciding;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// Applies the destination already decided for each occurrence. What the policy removes becomes [credencial] when it is a secret and [dado] otherwise; the pseudonymized stays as it
/// is, for the pseudonymization to replace later, and the preserved stays as it came (decision 0012). Occurrences that overlap leave as one span: when any of them is removed, the
/// whole span is, under one marker, so that no part of a removed value survives beside it.
/// </summary>
public sealed class DestinationsStep : ISanitizationStep
{
    /// <inheritdoc />
    public string Name => "destinos";

    /// <inheritdoc />
    public int Version => 1;

    /// <inheritdoc />
    public StepScope? Scope { get; } = new(
        "troca cada ocorrência que a política remove por um marcador do tipo: [credencial] para segredo, [dado] para o resto",
        "o pseudonimizado e o preservado, que ficam como vieram, e o texto fora das ocorrências");

    /// <inheritdoc />
    public IReadOnlyList<SanitizationEdit> Edits(StepInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var ordered = input.Occurrences
            .Where(decided => decided.Occurrence.Start < decided.Occurrence.End)
            .OrderBy(decided => decided.Occurrence.Start)
            .ThenBy(decided => decided.Occurrence.End)
            .ToArray();
        return [.. Groups(ordered)
            .Select(group => Removal(ordered.AsSpan(group.First, group.Count), group.End))
            .OfType<SanitizationEdit>()];
    }

    /// <summary>The runs of occurrences that overlap one another, each with where it starts in the order and where its last one ends.</summary>
    private static List<(int First, int Count, int End)> Groups(ReadOnlySpan<DecidedOccurrence> ordered)
    {
        var groups = new List<(int First, int Count, int End)>();
        var first = 0;
        var end = 0;
        for (var index = 0; index < ordered.Length; index++)
        {
            if (index > first && ordered[index].Occurrence.Start >= end)
            {
                groups.Add((first, index - first, end));
                first = index;
            }

            end = Math.Max(end, ordered[index].Occurrence.End);
        }

        groups.Add((first, ordered.Length - first, end));
        return groups;
    }

    private static SanitizationEdit? Removal(ReadOnlySpan<DecidedOccurrence> group, int end)
    {
        var removed = false;
        var secret = false;
        foreach (var member in group)
        {
            var removes = Removes(member.Destination);
            removed |= removes;
            secret |= removes && OccurrenceKinds.IsSecret(member.Occurrence.Kind);
        }

        if (!removed)
        {
            return null;
        }

        var marker = secret ? SanitizationMarker.Credential : SanitizationMarker.Data;
        return new SanitizationEdit(group[0].Occurrence.Start, end, marker);
    }

    private static bool Removes(Destination destination) => destination switch
    {
        Destination.Remove => true,
        Destination.Pseudonymize => false,
        Destination.Preserve => false,
        _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, "The destination is not known."),
    };
}
