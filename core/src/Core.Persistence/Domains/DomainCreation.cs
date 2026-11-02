using System.Data.Common;
using System.Globalization;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Platform.Keys;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Domains;

/// <summary>
/// The creation of a domain (decision 0045, issue 54): the owner credential writes the domain, the derivation labels of its two purposes and the event of the chain in one
/// serializable transaction, and either the three exist or none does. Before anything is written the custody has to hold both labels (operacao/002), since a domain whose
/// labels the custody lacks would write content nobody could open again. The identifier is the highest one plus one, written explicitly, so that the operator knows the labels
/// to add before the domain exists.
/// </summary>
public sealed class DomainCreation
{
    private const string NextId = "SELECT coalesce(pg_catalog.max(id), 0) + 1 FROM public.domain";
    private const string NameExists = "SELECT EXISTS (SELECT 1 FROM public.domain WHERE name = @name)";
    private const string Insert = "INSERT INTO public.domain (id, name) OVERRIDING SYSTEM VALUE VALUES (@id, @name)";
    private const string InsertLabel = "INSERT INTO public.derivation_label (label) VALUES (@label)";

    /// <summary>The identity of the table is moved to the highest identifier, so that a domain added without naming one does not take a number already used.</summary>
    private const string MoveIdentity = "SELECT pg_catalog.setval(pg_catalog.pg_get_serial_sequence('public.domain', 'id'), (SELECT pg_catalog.max(id) FROM public.domain))";

    private const string NameConstraint = "domain_name_key";
    private const int FirstVersion = 1;

    private readonly NpgsqlDataSource dataSource;
    private readonly Func<CustodyScope, CustodyEvent, CancellationToken, Task<CustodyResult>> append;

    /// <summary>The creation writes through the data source built with the credential of the owner, which is the one allowed to add a domain.</summary>
    public DomainCreation(NpgsqlDataSource dataSource)
        : this(dataSource, CustodyTransaction.AppendAsync)
    {
    }

    /// <summary>The creation with the way of adding the event given, so that a test can make the event fail after the domain and its labels are written.</summary>
    internal DomainCreation(NpgsqlDataSource dataSource, Func<CustodyScope, CustodyEvent, CancellationToken, Task<CustodyResult>> append)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(append);
        this.dataSource = dataSource;
        this.append = append;
    }

    /// <summary>The labels of derivation a domain with the identifier given takes, one for each purpose, in the order of the purposes.</summary>
    /// <param name="domainId">The identifier of the domain.</param>
    public static IReadOnlyList<string> LabelsOf(int domainId) => LabelsIn(domainId);

    private static List<string> LabelsIn(int domainId)
    {
        var labels = new List<string>();
        foreach (var purpose in Enum.GetValues<LabelPurpose>().Order())
        {
            labels.Add(new DerivationLabel(purpose, domainId, FirstVersion).Text);
        }

        return labels;
    }

    /// <summary>
    /// Creates the domain if the custody holds its labels; a refusal writes nothing, and a missing label is named with the identifier the domain would take.
    /// </summary>
    /// <param name="name">The name of the domain: not empty, and with no space around it.</param>
    /// <param name="custody">The derivation labels the custody file holds.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public Task<DomainCreationResult> CreateAsync(string name, DerivationLabels custody, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(custody);
        return name.Length == 0 || name != name.Trim()
            ? Task.FromResult(new DomainCreationResult { Outcome = DomainCreationOutcome.NameInvalid })
            : WriteAsync(name, custody, cancellationToken);
    }

    private async Task<DomainCreationResult> WriteAsync(string name, DerivationLabels custody, CancellationToken cancellationToken)
    {
        var answer = new Answer();
        _ = await CustodyTransaction.RunAsync(dataSource, scope => InScopeAsync(scope, (name, custody), answer, cancellationToken), cancellationToken).ConfigureAwait(false);
        return answer.Result ?? new DomainCreationResult { Outcome = DomainCreationOutcome.Conflict };
    }

    private async Task<CustodyResult?> InScopeAsync(CustodyScope scope, (string Name, DerivationLabels Custody) request, Answer answer, CancellationToken cancellationToken)
    {
        if (await NameTakenAsync(scope, request.Name, cancellationToken).ConfigureAwait(false))
        {
            answer.Result = new DomainCreationResult { Outcome = DomainCreationOutcome.NameTaken };
            return null;
        }

        var id = await NextIdAsync(scope, cancellationToken).ConfigureAwait(false);
        var lacking = request.Custody.Lacking(LabelsOf(id));
        if (lacking.Count > 0)
        {
            answer.Result = new DomainCreationResult { Outcome = DomainCreationOutcome.LabelsMissing, DomainId = id, MissingLabels = lacking };
            return null;
        }

        return await WriteAllAsync(scope, (id, request.Name), answer, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CustodyResult?> WriteAllAsync(CustodyScope scope, (int Id, string Name) domain, Answer answer, CancellationToken cancellationToken)
    {
        try
        {
            await WriteDomainAsync(scope, domain, cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException taken) when (taken.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            answer.Result = new DomainCreationResult
            {
                Outcome = taken.ConstraintName == NameConstraint ? DomainCreationOutcome.NameTaken : DomainCreationOutcome.Conflict,
            };
            return null;
        }

        var chain = await append(scope, CustodyEvent.DomainCreated(domain.Id), cancellationToken).ConfigureAwait(false);
        if (chain.Outcome != CustodyOutcome.Appended)
        {
            answer.Result = new DomainCreationResult { Outcome = ChainRefused(chain.Outcome), DomainId = domain.Id, ChainOutcome = chain.Outcome };
            return null;
        }

        await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
        answer.Result = new DomainCreationResult { Outcome = DomainCreationOutcome.Created, DomainId = domain.Id, Sequence = chain.Sequence };
        return chain;
    }

    /// <summary>A writer that met another at the same link of the chain was not refused by the chain: it may ask again.</summary>
    private static DomainCreationOutcome ChainRefused(CustodyOutcome outcome) =>
        outcome == CustodyOutcome.Conflict ? DomainCreationOutcome.Conflict : DomainCreationOutcome.ChainRefused;

    private static async Task WriteDomainAsync(CustodyScope scope, (int Id, string Name) domain, CancellationToken cancellationToken)
    {
        var insert = scope.CreateCommand(Insert);
        await using (insert.ConfigureAwait(false))
        {
            _ = insert.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Integer) { Value = domain.Id });
            _ = insert.Parameters.Add(new NpgsqlParameter("name", NpgsqlDbType.Text) { Value = domain.Name });
            _ = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var label in LabelsOf(domain.Id))
        {
            var command = scope.CreateCommand(InsertLabel);
            await using (command.ConfigureAwait(false))
            {
                _ = command.Parameters.Add(new NpgsqlParameter("label", NpgsqlDbType.Text) { Value = label });
                await InsertLabelAsync(command, domain.Id, cancellationToken).ConfigureAwait(false);
            }
        }

        var identity = scope.CreateCommand(MoveIdentity);
        await using (identity.ConfigureAwait(false))
        {
            _ = await identity.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>A label that exists with no domain to hold it is a registry that disagrees with itself, which a retry does not mend: it is a defect, not a conflict.</summary>
    private static async Task InsertLabelAsync(DbCommand command, int domainId, CancellationToken cancellationToken)
    {
        try
        {
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException taken) when (taken.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException($"A derivation label of the domain {domainId} exists already without the domain; the registry disagrees with itself.", taken);
        }
    }

    private static async Task<bool> NameTakenAsync(CustodyScope scope, string name, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(NameExists);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("name", NpgsqlDbType.Text) { Value = name });
            return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }

    private static async Task<int> NextIdAsync(CustodyScope scope, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(NextId);
        await using (command.ConfigureAwait(false))
        {
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }
    }

    private sealed class Answer
    {
        internal DomainCreationResult? Result { get; set; }
    }
}
