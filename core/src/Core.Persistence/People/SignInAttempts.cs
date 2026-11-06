using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.People;

/// <summary>
/// The record of the entry: every attempt to sign in, accepted or refused, is a row with the network prefix it came from and an event of the chain, written in the same transaction, so that
/// the attempt is either in both or in neither. The name typed and the password are never written; the series reads the refusals by prefix.
/// </summary>
public sealed class SignInAttempts
{
    private const int Tries = 3;
    private const int NetworkColumn = 0;
    private const int CountColumn = 1;
    private const int FirstColumn = 2;
    private const int LastColumn = 3;
    private const int Ipv4Kept = 3;
    private const int Ipv6Kept = 6;
    private const int Ipv4Length = 24;
    private const int Ipv6Length = 48;

    private readonly NpgsqlDataSource dataSource;

    /// <summary>The attempts are recorded in the database of the data source, in the role it logs in as.</summary>
    /// <param name="dataSource">The source of the connection, built with the credential of the application.</param>
    public SignInAttempts(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
    }

    /// <summary>The window of the series.</summary>
    public static TimeSpan Window { get; } = TimeSpan.FromHours(1);

    /// <summary>The least number of refusals from one prefix, within the window, that makes a series.</summary>
    public static int LeastRefusals { get; } = 10;

    /// <summary>Records an attempt that was accepted, authored by the person who signed in. Anything but <see cref="CustodyOutcome.Appended"/> means it was not recorded.</summary>
    public Task<CustodyResult> AcceptedAsync(long personId, IPAddress? origin, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(personId);
        return RecordAsync(attempt => CustodyEvent.SignInAccepted(personId, attempt), origin, cancellationToken);
    }

    /// <summary>Records an attempt that was refused. The person is given only for a wrong password, as its author; for any other reason the author is the system.</summary>
    public Task<CustodyResult> RefusedAsync(long? personId, SignInRefusal refusal, IPAddress? origin, CancellationToken cancellationToken)
    {
        var reason = refusal switch
        {
            SignInRefusal.PasswordWrong => CustodyReason.PasswordWrong,
            SignInRefusal.AccountNotRecognized => CustodyReason.AccountNotRecognized,
            _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "The refusal is not one the chain records."),
        };
        return RecordAsync(attempt => CustodyEvent.SignInRefused(personId, reason, attempt), origin, cancellationToken);
    }

    /// <summary>
    /// The prefixes that had <see cref="LeastRefusals"/> refusals or more in the <see cref="Window"/> that ends at <paramref name="until"/>, the most refused first. Reads only; adds no event.
    /// </summary>
    public async Task<IReadOnlyList<SignInSeries>> SeriesAsync(DateTimeOffset until, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(SignInStatements.Series);
        await using (command.ConfigureAwait(false))
        {
            PersonQueries.Add(command, "until", NpgsqlDbType.TimestampTz, until.UtcDateTime);
            PersonQueries.Add(command, "window", NpgsqlDbType.Interval, Window);
            PersonQueries.Add(command, "least", NpgsqlDbType.Integer, LeastRefusals);
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await SeriesOfAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>The prefix an attempt is recorded under: /24 of IPv4 or /48 of IPv6, the rest of the address zeroed, and null when the origin is unknown.</summary>
    public static string? NetworkOf(IPAddress? origin)
    {
        if (origin is null)
        {
            return null;
        }

        var address = origin.IsIPv4MappedToIPv6 ? origin.MapToIPv4() : origin;
        var bytes = address.GetAddressBytes();
        var version4 = address.AddressFamily == AddressFamily.InterNetwork;
        var kept = version4 ? Ipv4Kept : Ipv6Kept;
        Array.Clear(bytes, kept, bytes.Length - kept);
        return string.Create(CultureInfo.InvariantCulture, $"{new IPAddress(bytes)}/{(version4 ? Ipv4Length : Ipv6Length)}");
    }

    private static async Task<IReadOnlyList<SignInSeries>> SeriesOfAsync(NpgsqlDataReader reader, CancellationToken cancellationToken)
    {
        var series = new List<SignInSeries>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            series.Add(new SignInSeries(
                reader.GetString(NetworkColumn),
                reader.GetInt32(CountColumn),
                await reader.GetFieldValueAsync<DateTime>(FirstColumn, cancellationToken).ConfigureAwait(false),
                await reader.GetFieldValueAsync<DateTime>(LastColumn, cancellationToken).ConfigureAwait(false)));
        }

        return series;
    }

    private static async Task<long> InsertAsync(CustodyScope scope, string? network, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(SignInStatements.Insert);
        await using (command.ConfigureAwait(false))
        {
            PersonQueries.Add(command, "network", NpgsqlDbType.Text, network);
            return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }

    private async Task<CustodyResult> RecordAsync(Func<long, CustodyEvent> eventOf, IPAddress? origin, CancellationToken cancellationToken)
    {
        var network = NetworkOf(origin);
        var tries = 0;
        CustodyResult result;
        do
        {
            result = (await CustodyTransaction.AppendAfterAsync(
                dataSource,
                async scope => eventOf(await InsertAsync(scope, network, cancellationToken).ConfigureAwait(false)),
                cancellationToken).ConfigureAwait(false))!;
        }
        while (result.Outcome == CustodyOutcome.Conflict && ++tries < Tries);

        return result;
    }
}
