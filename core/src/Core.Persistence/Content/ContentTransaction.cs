using System.Data;
using System.Globalization;
using Core.Persistence.Custody.Chain;
using Npgsql;

namespace Core.Persistence.Content;

/// <summary>
/// A transaction in which every statement sees one domain, declared once when it opens: the database applies the row policy of each
/// content table to that domain, so no query writes a domain filter by hand. A session that does not go through here declares no
/// domain, and a content table answers it with no row.
/// </summary>
public sealed class ContentTransaction : IAsyncDisposable
{
    internal const string Declare = "SELECT set_config('app.domain', @domain, true)";

    /// <summary>
    /// Declares the domain and, with it, the scope of the system: the one the operations that only move keys or copy rows run in,
    /// which see the content of a batch that is not accepted too.
    /// </summary>
    internal const string DeclareSystem = "SELECT pg_catalog.set_config('app.domain', @domain, true), pg_catalog.set_config('app.content_scope', 'system', true)";

    private readonly NpgsqlConnection connection;
    private readonly NpgsqlTransaction transaction;

    private ContentTransaction(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        this.connection = connection;
        this.transaction = transaction;
    }

    /// <summary>Opens the transaction and declares the domain for its whole length; the declaration ends with the transaction, as SET LOCAL does.</summary>
    /// <param name="dataSource">The source of the connection, built with the credential that reads and writes the content.</param>
    /// <param name="domainId">The identifier of the domain in <c>public.domain</c>; a value that is not positive is refused before any connection opens.</param>
    /// <param name="cancellationToken">Cancels the opening.</param>
    public static Task<ContentTransaction> BeginAsync(NpgsqlDataSource dataSource, int domainId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(domainId);
        return OpenAsync(dataSource, domainId, Declare, cancellationToken);
    }

    /// <summary>As <see cref="BeginAsync(NpgsqlDataSource, int, CancellationToken)"/>, at the isolation level given.</summary>
    /// <param name="dataSource">The source of the connection, built with the credential that reads and writes the content.</param>
    /// <param name="domainId">The identifier of the domain in <c>public.domain</c>; a value that is not positive is refused before any connection opens.</param>
    /// <param name="isolation">The isolation level of the transaction.</param>
    /// <param name="cancellationToken">Cancels the opening.</param>
    internal static Task<ContentTransaction> BeginAsync(NpgsqlDataSource dataSource, int domainId, IsolationLevel isolation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(domainId);
        return OpenAsync(dataSource, new Declaration(domainId, Declare), isolation, cancellationToken);
    }

    /// <summary>
    /// As <see cref="BeginAsync(NpgsqlDataSource, int, CancellationToken)"/>, in the scope of the system, which sees every item and every map
    /// whatever the state of its batch: the rotation and the rewrap only wrap a key again and never open the content,
    /// and a batch accepted later must not be found with a key that no epoch holds. The reading of the stock never goes through here.
    /// </summary>
    /// <param name="dataSource">The source of the connection, built with the credential that reads and writes the content.</param>
    /// <param name="domainId">The identifier of the domain in <c>public.domain</c>.</param>
    /// <param name="cancellationToken">Cancels the opening.</param>
    internal static Task<ContentTransaction> BeginSystemAsync(NpgsqlDataSource dataSource, int domainId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(domainId);
        return OpenAsync(dataSource, new Declaration(domainId, DeclareSystem), IsolationLevel.ReadCommitted, cancellationToken);
    }

    /// <summary>The opening with the statement that declares the domain given, so that a test can make the declaration fail and see the connection released.</summary>
    internal static Task<ContentTransaction> OpenAsync(NpgsqlDataSource dataSource, int domainId, string declare, CancellationToken cancellationToken) =>
        OpenAsync(dataSource, new Declaration(domainId, declare), IsolationLevel.ReadCommitted, cancellationToken);

    private static async Task<ContentTransaction> OpenAsync(NpgsqlDataSource dataSource, Declaration declare, IsolationLevel isolation, CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var opened = false;
        try
        {
            var transaction = await connection.BeginTransactionAsync(isolation, cancellationToken).ConfigureAwait(false);
            var declaration = new NpgsqlCommand(declare.Statement, connection, transaction);
            await using (declaration.ConfigureAwait(false))
            {
                _ = declaration.Parameters.AddWithValue("domain", declare.DomainId.ToString(CultureInfo.InvariantCulture));
                _ = await declaration.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }

            opened = true;
            return new ContentTransaction(connection, transaction);
        }
        finally
        {
            if (!opened)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>Declares another domain for the rest of the transaction, so that one transaction can go through the domains one after the other.</summary>
    /// <param name="domainId">The identifier of the domain; a value that is not positive is refused.</param>
    /// <param name="cancellationToken">Cancels the declaration.</param>
    internal async Task ChangeDomainAsync(int domainId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(domainId);
        var declaration = CreateCommand(Declare);
        await using (declaration.ConfigureAwait(false))
        {
            _ = declaration.Parameters.AddWithValue("domain", domainId.ToString(CultureInfo.InvariantCulture));
            _ = await declaration.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Opens a transaction at the isolation level given under domain 0, which no row has: reads public tables, and content one domain at a time (<see cref="ChangeDomainAsync" />),
    /// in the scope of the system, for the copy of the data, which is faithful to every batch.
    /// </summary>
    internal static Task<ContentTransaction> BeginSnapshotAsync(NpgsqlDataSource dataSource, IsolationLevel isolation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        return OpenAsync(dataSource, new Declaration(0, DeclareSystem), isolation, cancellationToken);
    }

    /// <summary>The same transaction as the scope in which an event is added to the chain, so that the event and the change are written together.</summary>
    internal CustodyScope AsCustodyScope() => new(connection, transaction);

    /// <summary>A command that runs inside this transaction, under the domain it declared.</summary>
    /// <param name="sql">The statement; it never filters by domain.</param>
    public NpgsqlCommand CreateCommand(string sql) => new(sql, connection, transaction);

    /// <summary>Commits what the transaction wrote; without it, disposing rolls everything back.</summary>
    /// <param name="cancellationToken">Cancels the commit.</param>
    public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await transaction.DisposeAsync().ConfigureAwait(false);
        await connection.DisposeAsync().ConfigureAwait(false);
    }

    private readonly record struct Declaration(int DomainId, string Statement);
}
