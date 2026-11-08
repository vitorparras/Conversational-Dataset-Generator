using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.People;

/// <summary>
/// The notices of the people: what the system tells a person without any message leaving it. A notice is added with a code from a closed list and, for a password set again, the number of
/// sessions that ended; marking it as read takes it from the list of that person and keeps the row. A person lists and marks only their own: another person's identifier finds nothing.
/// </summary>
public sealed class PersonNotices
{
    private const int IdColumn = 0;
    private const int CodeColumn = 1;
    private const int SessionsColumn = 2;
    private const int CreatedColumn = 3;

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The notices are kept in the database of the data source, in the role it logs in as.</summary>
    /// <param name="dataSource">The source of the connection, built with the credential of the application.</param>
    public PersonNotices(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>Tells the person that their password was set again and how many sessions that ended; the database refuses a person who is not registered.</summary>
    /// <param name="personId">The person whose password was set again.</param>
    /// <param name="sessionsEnded">How many sessions the act ended, none or more.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public async Task RecordPasswordResetAsync(long personId, int sessionsEnded, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sessionsEnded);
        var command = dataSource.CreateCommand(NoticeStatements.Insert);
        await using (command.ConfigureAwait(false))
        {
            PersonQueries.Add(command, "person", NpgsqlDbType.Bigint, personId);
            PersonQueries.Add(command, "code", NpgsqlDbType.Text, NoticeStatements.PasswordReset);
            PersonQueries.Add(command, "sessions", NpgsqlDbType.Integer, sessionsEnded);
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The notices of the person that were not read, the newest first; empty when there is none.</summary>
    /// <param name="personId">The person whose notices are listed.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public async Task<IReadOnlyList<PersonNotice>> UnreadAsync(long personId, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(NoticeStatements.Unread);
        await using (command.ConfigureAwait(false))
        {
            PersonQueries.Add(command, "person", NpgsqlDbType.Bigint, personId);
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await NoticesOfAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Marks the notice as read; false when the person has no unread notice by that identifier, which is also the answer for the notice of another person.</summary>
    /// <param name="personId">The person who reads.</param>
    /// <param name="noticeId">The identifier of the notice.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public async Task<bool> MarkReadAsync(long personId, long noticeId, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(NoticeStatements.MarkRead);
        await using (command.ConfigureAwait(false))
        {
            PersonQueries.Add(command, "id", NpgsqlDbType.Bigint, noticeId);
            PersonQueries.Add(command, "person", NpgsqlDbType.Bigint, personId);
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
        }
    }

    private static NoticeCode CodeOf(string code) => code switch
    {
        NoticeStatements.PasswordReset => NoticeCode.PasswordReset,
        NoticeStatements.DomainReadOnly => NoticeCode.DomainReadOnly,
        _ => throw new InvalidOperationException("The database holds a notice code the code does not know."),
    };

    private static async Task<IReadOnlyList<PersonNotice>> NoticesOfAsync(NpgsqlDataReader reader, CancellationToken cancellationToken)
    {
        var notices = new List<PersonNotice>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var sessions = await reader.IsDBNullAsync(SessionsColumn, cancellationToken).ConfigureAwait(false) ? (int?)null : reader.GetInt32(SessionsColumn);
            var created = await reader.GetFieldValueAsync<DateTimeOffset>(CreatedColumn, cancellationToken).ConfigureAwait(false);
            notices.Add(new PersonNotice(reader.GetInt64(IdColumn), CodeOf(reader.GetString(CodeColumn)), sessions, created));
        }

        return notices;
    }
}
