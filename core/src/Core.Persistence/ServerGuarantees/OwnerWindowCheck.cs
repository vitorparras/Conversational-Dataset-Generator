using System.Globalization;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Verification;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Closes the window of the owner credential that a migration opened (#64), and runs only when one is open: the chain is verified, the guarantees are conferred and the system records the verdict
/// as the closing, with the application's credential. A guarantee missing, or a chain that does not hold and so does not take the event, records the closing as failed or not at all, refuses the
/// start naming the cause, and leaves the window open for the next start. It goes right after the privilege check, so that a start with the owner credential is refused first.
/// </summary>
public sealed class OwnerWindowCheck : IStartupCheck
{
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
                "refaça a garantia que falta com a credencial de dono, ou restaure uma cópia que a tenha, e suba o núcleo de novo; a janela segue aberta até uma partida em que tudo vale",
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
        var reference = string.Create(CultureInfo.InvariantCulture, $"opened={open.OpenedSequence};chain={chain.Recorded.Sequence ?? 0};conference={conferred.Recorded.Sequence ?? 0}");
        var closed = await new CustodyChain(dataSource).AppendAsync(CustodyEvent.OwnerWindowClosed((long)conferred.Missing, reference), cancellationToken).ConfigureAwait(false);
        var window = $"a janela da credencial de dono, aberta pela migração da versão {open.From} para a {open.To}";
        return (conferred.Missing, closed.Outcome) switch
        {
            (GuaranteeKinds.None, CustodyOutcome.Appended) => Finding.Holds($"{window}, foi fechada: a cadeia está íntegra e todas as garantias valem"),
            (GuaranteeKinds.None, _) => Finding.Missing($"{window}, não pôde fechar: a cadeia não está íntegra ou não aceitou o evento do fechamento"),
            _ => Finding.Missing($"{window}, não pôde fechar: faltam as garantias {conferred.Missing}; o fechamento ficou registrado como reprovado na cadeia"),
        };
    }
}
