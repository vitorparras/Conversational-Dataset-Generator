using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Core.Persistence.People;
using Core.Platform.Sessions;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>
/// The limits of the entries in progress (#71, decision 0048): 4 for each network prefix and 64 for the host, reserved in one step before the password is checked and given back after the
/// answer. With no place left the entry is refused before the password is checked, with the answer of any refusal, so a full limit is neither a cheap attempt nor a verdict.
/// The check of the password is a database that does not answer or does not exist: reaching it is seen from the outside, and not reaching it answers.
/// </summary>
public sealed class SignInSaturationTests
{
    private const int Parallel = 8;
    private static readonly IPAddress Here = SignInDelayTests.NetworkNumber(1);

    [Fact]
    public void FourEntriesInProgressForOnePrefixFillThatPrefixOnlyAndTheFifthGetsNoPlaceUntilOneLeaves()
    {
        var delay = new SignInDelay(new ManualClock());
        var places = Enumerable.Range(0, SignInDelay.MaxWaitingPerOrigin).Select(_ => delay.Admit(Here)).ToList();

        Assert.All(places, Assert.NotNull);
        Assert.Null(delay.Admit(Here));
        using var elsewhere = delay.Admit(SignInDelayTests.NetworkNumber(2));
        Assert.NotNull(elsewhere);
        places[0]!.Dispose();
        places[0]!.Dispose();
        using var again = delay.Admit(Here);
        Assert.NotNull(again);
        Assert.Null(delay.Admit(Here));
    }

    [Fact]
    public void SixtyFourEntriesInProgressFillTheHostWhateverTheOriginAndTheNextGetsNoPlaceUntilOneLeaves()
    {
        var delay = new SignInDelay(new ManualClock());
        var places = Enumerable.Range(0, SignInDelay.MaxWaiting).Select(index => delay.Admit(SignInDelayTests.NetworkNumber(index))).ToList();

        Assert.All(places, Assert.NotNull);
        Assert.Null(delay.Admit(SignInDelayTests.NetworkNumber(900)));
        places[10]!.Dispose();
        using var again = delay.Admit(SignInDelayTests.NetworkNumber(900));
        Assert.NotNull(again);
    }

    [Fact]
    public void AnEntryThatIsNotCheckedStillCountsAsARefusalSoTheDelayKeepsRising()
    {
        var delay = new SignInDelay(new ManualClock());

        for (var refusal = 0; refusal < 8; refusal++)
        {
            delay.Counted("ana", Here);
        }

        Assert.False(delay.RefusedAsync("ana", Here, TestContext.Current.CancellationToken).IsCompleted);
    }

    [Fact]
    public async Task EightEntriesInParallelFromOneOriginWithTheCheckBlockedLetNoMoreThanFourReachIt()
    {
        var clock = new ManualClock();
        var delay = new SignInDelay(clock);
        using var database = new SilentDatabase();
        var signIn = Entry(database.ConnectionString, clock, delay);
        using var cancel = new CancellationTokenSource();

        var entries = Enumerable.Range(0, Parallel)
            .Select(index => signIn.OpenAsync($"ana-{index}", "a senha certa ou errada", Here, cancel.Token)).ToList();
        await database.WaitForAsync(SignInDelay.MaxWaitingPerOrigin, TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        var refusedAtOnce = entries.Where(entry => entry.IsCompletedSuccessfully).ToList();
        Assert.Equal((SignInDelay.MaxWaitingPerOrigin, Parallel - SignInDelay.MaxWaitingPerOrigin), (database.Connections, refusedAtOnce.Count));
        Assert.All(refusedAtOnce, entry => Assert.Null(entry.Result));
        await cancel.CancelAsync();
        foreach (var blocked in entries.Except(refusedAtOnce))
        {
            _ = await Assert.ThrowsAnyAsync<Exception>(() => blocked);
        }
    }

    [Fact]
    public async Task WithNoPlaceLeftAnywhereTheEntryIsRefusedWithoutCheckingThePasswordWhateverThePassword()
    {
        var clock = new ManualClock();
        var delay = new SignInDelay(clock);
        var places = Enumerable.Range(0, SignInDelay.MaxWaiting).Select(index => delay.Admit(SignInDelayTests.NetworkNumber(index))).ToList();
        using var database = new SilentDatabase();
        var signIn = Entry(database.ConnectionString, clock, delay);

        var fromElsewhere = await signIn.OpenAsync("bia", "a senha certa ou errada", SignInDelayTests.NetworkNumber(700), TestContext.Current.CancellationToken);

        Assert.Null(fromElsewhere);
        Assert.Equal(0, database.Connections);
        places.ForEach(place => place!.Dispose());
    }

    [Fact]
    public async Task WithPlacesLeftTheEntryReachesTheCheckOfThePasswordAndGivesItsPlaceBack()
    {
        var clock = new ManualClock();
        var delay = new SignInDelay(clock);
        var signIn = Entry("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1;Pooling=false", clock, delay);

        for (var attempt = 0; attempt < SignInDelay.MaxWaitingPerOrigin + 1; attempt++)
        {
            _ = await Assert.ThrowsAnyAsync<Exception>(() => signIn.OpenAsync("ana", "senha", Here, TestContext.Current.CancellationToken));
        }
    }

    [SuppressMessage("Reliability", "CA2000", Justification = "The data source never connects to anything real, so it holds nothing to release.")]
    private static SignIn Entry(string connectionString, TimeProvider clock, SignInDelay delay)
    {
        var source = NpgsqlDataSource.Create(connectionString);
        var sessions = new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), clock);
        return new SignIn(new PersonRegistry(source), new PersonCredentials(source, new PasswordDerivation(1)), sessions, delay);
    }

    /// <summary>A server that takes the connection and never answers, so a check of the password that reaches it stays blocked, and counts how many reached it.</summary>
    private sealed class SilentDatabase : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly List<TcpClient> held = [];
        private int connections;

        public SilentDatabase()
        {
            listener.Start();
            _ = Task.Run(AcceptAsync);
        }

        public int Connections => Volatile.Read(ref connections);

        public string ConnectionString =>
            $"Host=127.0.0.1;Port={((IPEndPoint)listener.LocalEndpoint).Port};Database=none;Username=none;Password=none;Timeout=60;Command Timeout=60;Pooling=false";

        public async Task WaitForAsync(int expected, CancellationToken cancellationToken)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(TimeSpan.FromSeconds(20));
            while (Connections < expected)
            {
                await Task.Delay(20, limit.Token);
            }
        }

        public void Dispose()
        {
            listener.Dispose();
            lock (held)
            {
                held.ForEach(client => client.Dispose());
            }
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (true)
                {
                    var client = await listener.AcceptTcpClientAsync();
                    lock (held)
                    {
                        held.Add(client);
                    }

                    _ = Interlocked.Increment(ref connections);
                }
            }
            catch (Exception ex) when (ex is ObjectDisposedException or SocketException or InvalidOperationException)
            {
                // The listener was stopped: the test is over.
            }
        }
    }
}
