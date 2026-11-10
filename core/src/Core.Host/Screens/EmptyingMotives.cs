using Core.Persistence.Content.Emptying;

namespace Core.Host.Screens;

/// <summary>
/// The motives of an emptying as the screens offer and show them: for each motive the emptying takes, the value the form sends and the words a person reads.
/// The list of motives is the one of <see cref="EmptyingMotive"/>; nothing here decides which motive may empty.
/// </summary>
internal static class EmptyingMotives
{
    internal static IReadOnlyList<ChoiceGroup.Option> Options { get; } =
        Array.ConvertAll(Enum.GetValues<EmptyingMotive>(), motive => new ChoiceGroup.Option(ValueOf(motive), CaptionOf(motive)));

    internal static string CaptionOf(EmptyingMotive motive) => motive switch
    {
        EmptyingMotive.RemovalRequestedBySubject => "Pedido de remoção da pessoa de quem o conteúdo fala",
        EmptyingMotive.ContractRequirement => "Exigência do contrato com quem entregou o conteúdo",
        _ => throw new ArgumentOutOfRangeException(nameof(motive), motive, "The motive has no words on the screens."),
    };

    /// <summary>The motive the form sent, none when it sent nothing or something no motive is.</summary>
    internal static EmptyingMotive? Parse(string? value)
    {
        foreach (var motive in Enum.GetValues<EmptyingMotive>())
        {
            if (string.Equals(ValueOf(motive), value, StringComparison.Ordinal))
            {
                return motive;
            }
        }

        return null;
    }

    private static string ValueOf(EmptyingMotive motive) => motive switch
    {
        EmptyingMotive.RemovalRequestedBySubject => "pedido-de-remocao",
        EmptyingMotive.ContractRequirement => "exigencia-do-contrato",
        _ => throw new ArgumentOutOfRangeException(nameof(motive), motive, "The motive has no value on the screens."),
    };
}
