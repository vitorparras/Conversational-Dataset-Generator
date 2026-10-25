using System.Diagnostics;
using Core.Boundary.Contracts;

namespace Core.Host.Analysis;

/// <summary>An analysis that did not conclude: the reference to resume it by, the reason, and the cause as the operator reads it; it carries no analysis to keep.</summary>
/// <param name="Reference">The opaque reference of the interrupted analysis, which a retry names to resume it.</param>
/// <param name="Reason">Why the recognizer gave no analysis.</param>
/// <param name="Cause">What happened to the material and why, in the words of the operator.</param>
internal sealed record RefusedAnalysis(Guid Reference, UnavailabilityReason Reason, string Cause)
{
    /// <summary>The refusal for the reason, with the cause that names it.</summary>
    internal static RefusedAnalysis For(Guid reference, UnavailabilityReason reason) =>
        new(reference, reason, "Material não analisado e nada gravado: " + CauseOf(reason));

    private static string CauseOf(UnavailabilityReason reason) => reason switch
    {
        UnavailabilityReason.TimedOut => "o serviço de linguagem não respondeu dentro do tempo limite.",
        UnavailabilityReason.ConnectionRefused => "o serviço de linguagem não pôde ser alcançado.",
        UnavailabilityReason.MalformedResponse =>
            "o serviço de linguagem não deu resposta no formato do contrato. Ele recusou o pedido, respondeu fora do " +
            "contrato ou a conexão caiu no meio da resposta, como acontece quando o serviço encerra com o pedido em curso.",
        UnavailabilityReason.UnknownContractVersion => "o serviço de linguagem fala uma versão do contrato que o núcleo não conhece.",
        _ => throw new UnreachableException(),
    };
}
