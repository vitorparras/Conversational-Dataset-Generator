namespace Core.Host.Screens;

/// <summary>
/// The words of the review by sample (#610): the version of the text an item shows and the kinds of personal data a finding names. A finding is recorded by its kind alone,
/// never by the data found, so that the register of the review holds no personal data either.
/// </summary>
internal static class SampleTexts
{
    private static readonly ChoiceGroup.Option[] Kinds =
    [
        new("nome", "Nome de pessoa"),
        new("documento", "Número de documento"),
        new("contato", "Contato: telefone, e-mail ou perfil"),
        new("endereco", "Endereço ou localização de uma pessoa"),
        new("sensivel", "Dado sensível: saúde, religião, origem ou orientação"),
        new("outro", "Outro dado que identifica uma pessoa"),
    ];

    /// <summary>The kinds of personal data a finding names, with the code the register keeps.</summary>
    internal static IReadOnlyList<ChoiceGroup.Option> Findings => Kinds;

    /// <summary>Which version of the text the item shows: the most treated that exists.</summary>
    internal static string VersionOf(bool cleaned) =>
        cleaned ? "Texto sanitizado, a versão mais tratada que existe." : "Texto bruto: o item ainda não tem versão sanitizada.";

    /// <summary>The kind of a finding in words; one the screen does not know is said by its name.</summary>
    internal static string FindingOf(string code) => Array.Find(Kinds, kind => kind.Value == code)?.Caption ?? $"Achado registrado: {code}";
}
