namespace Core.Host;

/// <summary>
/// The warning of the manual situation: nothing but the person who operates puts a seal outside the base, so a seal that was never recorded, or recorded more than seven days ago, is told
/// along with the verification. The other situations send the seal themselves, and their failure is told in the log of the host.
/// </summary>
internal static class SealAge
{
    internal static readonly TimeSpan Limit = TimeSpan.FromDays(7);

    /// <summary>The warning in words; null when the situation is not the manual one or the last seal is not older than the limit.</summary>
    /// <param name="situation">What the installation declared.</param>
    /// <param name="lastSeal">When the chain last recorded a seal; null when it never did.</param>
    /// <param name="now">The moment of the answer.</param>
    internal static string? Warning(SealSituation situation, DateTimeOffset? lastSeal, DateTimeOffset now)
    {
        if (situation != SealSituation.Manual)
        {
            return null;
        }

        if (lastSeal is not { } recorded)
        {
            return "Nenhum selo foi registrado: emita um em GET /custody/seal, guarde-o fora do alcance da base e confirme em POST /custody/seal/confirmation.";
        }

        return now - recorded > Limit ? "O último selo registrado tem mais de 7 dias: emita outro, guarde-o fora do alcance da base e confirme-o." : null;
    }
}
