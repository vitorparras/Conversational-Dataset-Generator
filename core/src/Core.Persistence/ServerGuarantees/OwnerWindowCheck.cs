using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Verification;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Closes the window of the owner credential that a migration opened (#64), and runs only when one is open: the chain is verified, the guarantees of the server are conferred, and the system records
/// the verdict as the closing of the window, with the application's credential. When a guarantee is missing the closing is recorded as failed and the start is refused, naming the guarantee;
/// the window stays open and the next start tries again. A chain that does not hold is recorded the same way but does not refuse the start, because reading must go on to investigate it.
/// It goes right after the privilege check, so that a start with the owner credential is refused first, and before the checks of the guarantees it confers itself.
/// </summary>
public sealed class OwnerWindowCheck : IStartupCheck
{
    private const long ChainNotIntact = 64;
    private const string NoTable = "42P01";

    private readonly NpgsqlDataSource dataSource;
    private readonly GuaranteeVerifier guarantees;
    private readonly TimeProvider time;

    /// <summary>The check reads and records through the data source built with the application's credential, and confers the guarantees with the verifier it is given.</summary>
    public OwnerWindowCheck(NpgsqlDataSource dataSource, GuaranteeVerifier guarantees, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(guarantees);
        ArgumentNullException.ThrowIfNull(time);
        this.dataSource = dataSource;
        this.guarantees = guarantees;
        this.time = time;
    }

    /// <inheritdoc />
    public string Name => "OwnerWindow";

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken) =>
        ServerProbe.RunAsync(
            dataSource,
            new Guarantee(
                Name,
                "refaça a garantia que falta com a credencial de dono, ou restaure uma cópia que a tenha, e suba o núcleo de novo; a janela segue aberta até uma partida em que todas valem",
                (_, token) => CloseAsync(token),
                ReadsCatalog: false),
            cancellationToken);

    private async Task<Finding> CloseAsync(CancellationToken cancellationToken)
    {
        OwnerWindowOpen? open;
        try
        {
            open = await new RecordedVerificationReader(dataSource).OwnerWindowAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException noChain) when (noChain.SqlState == NoTable)
        {
            return Finding.Holds("o banco ainda não tem a cadeia de custódia, e sem ela não há janela da credencial de dono");
        }

        if (open is null)
        {
            return Finding.Holds("nenhuma janela da credencial de dono está aberta");
        }

        var chain = await new CustodyVerifier(dataSource, time).VerifyAsync(cancellationToken).ConfigureAwait(false);
        var conferred = await guarantees.VerifyAsync(cancellationToken).ConfigureAwait(false);
        var missing = (long)conferred.Missing | (chain.Passed ? 0 : ChainNotIntact);
        var reference = OwnerWindowReference.Closed(open.OpenedSequence, chain.Recorded.Sequence ?? 0, conferred.Recorded.Sequence ?? 0);
        var closed = await new CustodyChain(dataSource).AppendAsync(CustodyEvent.OwnerWindowClosed(missing, reference), cancellationToken).ConfigureAwait(false);
        return Judge(open, conferred.Missing, chain.Passed, closed.Outcome == CustodyOutcome.Appended);
    }

    private static Finding Judge(OwnerWindowOpen open, GuaranteeKinds missing, bool chainHeld, bool recorded)
    {
        var window = $"a janela da credencial de dono, aberta pela migração da versão {open.From} para a {open.To}";
        if (missing != GuaranteeKinds.None)
        {
            return Finding.Missing($"{window}, não pôde fechar: faltam as garantias {missing}; o fechamento ficou registrado como reprovado na cadeia");
        }

        if (!recorded)
        {
            return Finding.Missing($"{window}, não pôde fechar: a cadeia não aceitou o evento do fechamento");
        }

        return chainHeld
            ? Finding.Holds($"{window}, foi fechada: a cadeia está íntegra e todas as garantias valem")
            : Finding.Holds($"{window}, segue aberta: a cadeia não está íntegra, o fechamento ficou registrado como reprovado e a janela aparece em /custody/verification");
    }
}
