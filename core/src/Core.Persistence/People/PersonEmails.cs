using System.Net.Mail;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.People;

/// <summary>
/// The e-mail address of a person who operates (#635, policy 009): optional, kept only to carry the link that resets a password, and removed by the database when the person is deactivated.
/// Nothing here writes the address to a log, an event of the chain or a metric; it goes to the database as a parameter and comes back to the one screen that shows it.
/// </summary>
public sealed class PersonEmails
{
    private const int Longest = 254;
    private const int Shortest = 3;
    private const string Read = "SELECT address FROM public.person_email WHERE person_id = @person";
    private const string Keep =
        "INSERT INTO public.person_email (person_id, address) VALUES (@person, @address) ON CONFLICT (person_id) DO UPDATE SET address = EXCLUDED.address, changed_at = clock_timestamp()";
    private const string Forget = "DELETE FROM public.person_email WHERE person_id = @person";
    private const string PersonParameter = "person";
    private readonly NpgsqlDataSource dataSource;

    /// <summary>The addresses are kept in the database of the data source, in the role it logs in as.</summary>
    public PersonEmails(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>The most characters an address may have, as the database holds it.</summary>
    public static int MostCharacters => Longest;

    /// <summary>Whether the text is an address the registry takes: one mailbox, without a name around it, without white space and within the size the database holds.</summary>
    /// <param name="address">The address as typed, already trimmed.</param>
    public static bool IsAddress(string address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return IsShaped(address) && MailAddress.TryCreate(address, out var parsed) && string.Equals(parsed.Address, address, StringComparison.Ordinal);
    }

    /// <summary>The address of the person; null when the person has none.</summary>
    public async Task<string?> AddressOfAsync(long personId, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(Read);
        await using (command.ConfigureAwait(false))
        {
            PersonQueries.Add(command, PersonParameter, NpgsqlDbType.Bigint, personId);
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        }
    }

    /// <summary>Keeps the address of the person, in place of the one before; none removes it. An address the registry does not take is refused before the database is asked.</summary>
    /// <param name="personId">The person.</param>
    /// <param name="address">The address, or none to remove it.</param>
    /// <param name="cancellationToken">Cancels the writing.</param>
    public Task SetAsync(long personId, string? address, CancellationToken cancellationToken)
    {
        if (address is not null && !IsAddress(address))
        {
            throw new ArgumentException("The address is not one the registry takes.", nameof(address));
        }

        return SettingAsync(personId, address, cancellationToken);
    }

    /// <summary>Within the sizes, with no white space and with one at sign, as the constraint of the database asks.</summary>
    private static bool IsShaped(string address) =>
        address.Length is >= Shortest and <= Longest && !address.Any(char.IsWhiteSpace) && address.Count(letter => letter == '@') == 1;

    private async Task SettingAsync(long personId, string? address, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(address is null ? Forget : Keep);
        await using (command.ConfigureAwait(false))
        {
            PersonQueries.Add(command, PersonParameter, NpgsqlDbType.Bigint, personId);
            if (address is not null)
            {
                PersonQueries.Add(command, "address", NpgsqlDbType.Text, address);
            }

            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
