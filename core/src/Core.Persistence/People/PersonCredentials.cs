using Core.Persistence.Custody;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.People;

/// <summary>
/// The passwords of the people. A password is kept as what its derivation left, with a salt of each person, and is checked in constant time. When a person enters with the right password
/// and the work factor stored is lower than the current one, the derivation is made again with the current factor. No password, salt or result reaches a log or an exception.
/// </summary>
public sealed class PersonCredentials
{
    private const int AlgorithmColumn = 0;
    private const int FactorColumn = 1;
    private const int SaltColumn = 2;
    private const int DerivedColumn = 3;

    private readonly NpgsqlDataSource dataSource;
    private readonly PasswordDerivation derivation;

    /// <summary>The store writes through the data source built with the application's credential and derives with the work factor of the derivation given.</summary>
    public PersonCredentials(NpgsqlDataSource dataSource, PasswordDerivation derivation)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(derivation);
        this.dataSource = dataSource;
        this.derivation = derivation;
    }

    /// <summary>Sets the password of an active person, replacing the one they had, and records the act without any value of it; a refusal writes nothing.</summary>
    /// <param name="personId">The person whose password is set.</param>
    /// <param name="password">The password; it must not be empty.</param>
    /// <param name="actorId">The person who acts, or null when nobody acts as a person.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public Task<PersonResult> SetPasswordAsync(long personId, string password, long? actorId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);
        return ReplaceAsync(personId, password, actorId, cancellationToken);
    }

    /// <summary>Whether the password is the one of an active person; when it is and the work factor stored is lower than the current, the derivation is made again with the current one.</summary>
    /// <param name="personId">The person who asks to enter.</param>
    /// <param name="password">The password offered.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    public Task<PasswordOutcome> VerifyAsync(long personId, string password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(password);
        return CheckAsync(personId, password, cancellationToken);
    }

    private static PasswordOutcome Judge(bool? active, Credential? stored, bool matches) => (active, stored, matches) switch
    {
        (null, _, _) => PasswordOutcome.Unknown,
        (false, _, _) => PasswordOutcome.Deactivated,
        (_, null, _) => PasswordOutcome.NoPassword,
        (_, _, false) => PasswordOutcome.Wrong,
        _ => PasswordOutcome.Accepted,
    };

    private static async Task<Credential?> ReadAsync(NpgsqlDataSource source, long personId, CancellationToken cancellationToken)
    {
        var command = source.CreateCommand(CredentialStatements.Read);
        await using (command.ConfigureAwait(false))
        {
            PersonQueries.Add(command, "id", NpgsqlDbType.Bigint, personId);
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await CredentialOfAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<Credential?> CredentialOfAsync(NpgsqlDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var salt = await reader.GetFieldValueAsync<byte[]>(SaltColumn, cancellationToken).ConfigureAwait(false);
        var derived = await reader.GetFieldValueAsync<byte[]>(DerivedColumn, cancellationToken).ConfigureAwait(false);
        return new Credential(reader.GetString(AlgorithmColumn), reader.GetInt32(FactorColumn), salt, derived);
    }

    /// <summary>A refusal of the server may carry the row that failed, which here is the salt and the result; only the code of the refusal leaves.</summary>
    private static async Task<T> WithoutRowAsync<T>(Func<Task<T>> write)
    {
        try
        {
            return await write().ConfigureAwait(false);
        }
        catch (PostgresException refused)
        {
            throw new InvalidOperationException($"The server refused to write the credential (SQLSTATE {refused.SqlState}).");
        }
    }

    private static void Bind(NpgsqlCommand command, long personId, Credential credential)
    {
        PersonQueries.Add(command, "id", NpgsqlDbType.Bigint, personId);
        PersonQueries.Add(command, "algorithm", NpgsqlDbType.Text, credential.Algorithm);
        PersonQueries.Add(command, "factor", NpgsqlDbType.Integer, credential.WorkFactor);
        PersonQueries.Add(command, "salt", NpgsqlDbType.Bytea, credential.Salt);
        PersonQueries.Add(command, "derived", NpgsqlDbType.Bytea, credential.Derived);
    }

    private async Task<PersonResult> ReplaceAsync(long personId, string password, long? actorId, CancellationToken cancellationToken)
    {
        if (password.Length == 0)
        {
            return PersonRefusals.PasswordInvalid();
        }

        var credential = derivation.Derive(password);
        var statement = new PersonStatement(
            CredentialStatements.Set,
            command =>
            {
                Bind(command, personId, credential);
                PersonQueries.Add(command, "actor", NpgsqlDbType.Bigint, actorId);
            },
            CustodyStep.PersonPasswordSet,
            personId);
        return await WithoutRowAsync(() => dataSource.WriteChainedAsync(statement, actorId, cancellationToken)).ConfigureAwait(false)
            ?? await WhyNotSetAsync(personId, actorId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PersonResult> WhyNotSetAsync(long personId, long? actorId, CancellationToken cancellationToken) =>
        await dataSource.ActorRefusalAsync(actorId, cancellationToken).ConfigureAwait(false)
        ?? (await dataSource.ReadActiveAsync(personId, cancellationToken).ConfigureAwait(false) is null ? PersonRefusals.NotFound() : PersonRefusals.Deactivated());

    private async Task<PasswordOutcome> CheckAsync(long personId, string password, CancellationToken cancellationToken)
    {
        var active = await dataSource.ReadActiveAsync(personId, cancellationToken).ConfigureAwait(false);
        var stored = active is true ? await ReadAsync(dataSource, personId, cancellationToken).ConfigureAwait(false) : null;
        var outcome = Judge(active, stored, derivation.Matches(stored, password));
        if (outcome == PasswordOutcome.Accepted && derivation.IsBehind(stored!))
        {
            await RecomputeAsync(personId, stored!, password, cancellationToken).ConfigureAwait(false);
        }

        return outcome;
    }

    private async Task RecomputeAsync(long personId, Credential previous, string password, CancellationToken cancellationToken)
    {
        var current = derivation.Derive(password);
        _ = await WithoutRowAsync(() => dataSource.WriteAsync(
            CredentialStatements.Recompute,
            command =>
            {
                Bind(command, personId, current);
                PersonQueries.Add(command, "previous", NpgsqlDbType.Bytea, previous.Derived);
            },
            cancellationToken)).ConfigureAwait(false);
    }
}
