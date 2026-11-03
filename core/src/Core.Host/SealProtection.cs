using System.Xml;

namespace Core.Host;

/// <summary>
/// What a verification against the seal protects, said for the situation the installation declared and never as integrity without a qualification: the external situation guards against a base
/// replaced and an end removed, the internal one against a restoration by mistake and against whoever reaches only the database, and the manual one attests coherence since the last seal.
/// </summary>
internal static class SealProtection
{
    private const string DeclaredOnly =
        "O núcleo não confere se o destino está fora do controle da base: isso é declaração da instalação, sem prova.";

    /// <summary>The words for the situation; the manual one carries the date of the last seal, or says there is none.</summary>
    /// <param name="situation">What the installation declared.</param>
    /// <param name="lastSeal">The moment of the last seal, which the manual situation says; null when there is none.</param>
    internal static string Of(SealSituation situation, DateTimeOffset? lastSeal) => situation switch
    {
        SealSituation.External =>
            "Protege contra a base substituída por outra coerente e contra a remoção do fim da cadeia, "
            + "desde que o selo guardado no destino externo declarado não tenha sido alterado por quem controla a base. Não protege contra quem controla também esse destino. "
            + DeclaredOnly,
        SealSituation.Internal =>
            "Protege contra uma restauração equivocada da base e contra quem alcança só o banco. "
            + "Não protege contra quem controla a rede onde está o destino declarado, nem o próprio destino. "
            + DeclaredOnly,
        SealSituation.Manual => ManualWords(lastSeal),
        _ => throw new ArgumentOutOfRangeException(nameof(situation)),
    };

    private static string ManualWords(DateTimeOffset? lastSeal) =>
        lastSeal is { } moment
            ? "A comparação com o selo de " + InWholeSeconds(moment) + " cobre a cadeia até essa data. Desde ela, a verificação atesta só a coerência dos elos, e não a integridade: "
              + "nenhum selo confirma o que entrou depois."
            : "Nenhum selo foi registrado ainda: sem ele, a verificação só diz que os elos da cadeia se fecham, e não atesta integridade.";

    /// <summary>The moment in UTC without the fraction of a second, as the text of a seal writes it.</summary>
    private static string InWholeSeconds(DateTimeOffset moment) =>
        XmlConvert.ToString(moment.UtcDateTime.AddTicks(-(moment.UtcDateTime.Ticks % TimeSpan.TicksPerSecond)), XmlDateTimeSerializationMode.Utc);
}
