using Core.Boundary.Contracts;

namespace Core.Ingestion.Sanitizing;

/// <summary>
/// Removes the closed forms of identifier the policy always removes, whether or not the entrance marked them: a document number, a bank account, a plate or a device identifier
/// becomes [documento], a telephone or an electronic address becomes [contato]. It is form, not judgement, and it reads the form across what splits it, an invisible character, an
/// odd separator or irregular spacing, taking the separators away with it; money, quantities, dates and times stay (policy 001, requirement 004). An occurrence that touches what it
/// removes, wholly or in part, leaves with it, under the one marker, whatever its destination.
/// </summary>
public sealed class IdentifierStep : ISanitizationStep
{
    private readonly ITextBoundaries boundaries;

    /// <summary>The removal, which reads the text by the code points the boundary gives.</summary>
    /// <param name="boundaries">Splits the text into its code points.</param>
    public IdentifierStep(ITextBoundaries boundaries)
    {
        ArgumentNullException.ThrowIfNull(boundaries);
        this.boundaries = boundaries;
    }

    /// <inheritdoc />
    public string Name => "identificadores";

    /// <inheritdoc />
    public int Version => 1;

    /// <inheritdoc />
    public StepScope? Scope { get; } = new(
        "troca por [documento] o número de documento, a conta bancária, a placa e o identificador de aparelho, e por [contato] o telefone e o endereço eletrônico, mesmo partidos por "
            + "caractere invisível ou separador incomum, com os separadores e a ocorrência que os toca",
        "o valor em moeda, a quantidade com unidade, a data, a hora, o número de menos de oito dígitos e todo o texto fora do identificador");

    /// <inheritdoc />
    public IReadOnlyList<SanitizationEdit> Edits(StepInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var text = IdentifierRunes.Of(boundaries.CodePoints(input.Text));
        var touching = input.Occurrences
            .Where(decided => decided.Occurrence.Start < decided.Occurrence.End)
            .Select(decided => new IdentifierSpan(decided.Occurrence.Start, decided.Occurrence.End, null));
        return [.. IdentifierSpan.Joined([.. IdentifierNumbers.Find(text), .. IdentifierWords.Find(text), .. touching]).Select(span => new SanitizationEdit(span.Start, span.End, span.Marker))];
    }
}
