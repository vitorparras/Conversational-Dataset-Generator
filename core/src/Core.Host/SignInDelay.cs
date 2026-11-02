using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Core.Host;

/// <summary>
/// The delay of a refused entry (decision 0048): from the second refusal on, the answer waits 1 s, doubling up to 30 s, by the greater of two counters,
/// the one of the name typed and the one of the network prefix it came from. Both live in memory only; the name is kept as an HMAC under a key made at start, never as typed.
/// </summary>
internal sealed class SignInDelay
{
    internal const int MaxWaiting = 64;
    internal const int MaxWaitingPerOrigin = 4;
    internal const int MaxCounters = 10_000;
    internal static readonly TimeSpan First = TimeSpan.FromSeconds(1);
    internal static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan Forgetting = TimeSpan.FromMinutes(15);

    private const string Unknown = "none";
    private const int FirstDelayed = 2;
    private const int Doublings = 5;
    private const int Ipv4PrefixBytes = 3;
    private const int Ipv6PrefixBytes = 6;
    private static readonly TimeSpan SweepEvery = TimeSpan.FromMinutes(1);

    private readonly TimeProvider clock;
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    private readonly Lock gate = new();
    private readonly Dictionary<string, Streak> typed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Streak> origins = new(StringComparer.Ordinal);
    private DateTimeOffset swept;
    private readonly Dictionary<string, int> waitingByOrigin = new(StringComparer.Ordinal);
    private int waiting;

    internal SignInDelay(TimeProvider clock)
    {
        this.clock = clock;
        swept = clock.GetUtcNow();
    }

    /// <summary>
    /// Reserves, in one step, a place among the entries in progress: 4 for the network prefix and 64 for the host. The place is kept until the admission is disposed, after the
    /// answer, so that the entries in parallel cannot all pass before the first one is counted. Null when a limit is full: the entry is then refused without the password being
    /// checked, with the answer of any refusal, so that whoever fills the limits gets neither a cheap attempt nor a verdict.
    /// </summary>
    internal Admission? Admit(IPAddress? origin)
    {
        var prefix = OriginKey(origin);
        lock (gate)
        {
            var byOrigin = waitingByOrigin.GetValueOrDefault(prefix);
            if (waiting >= MaxWaiting || byOrigin >= MaxWaitingPerOrigin)
            {
                return null;
            }

            waiting++;
            waitingByOrigin[prefix] = byOrigin + 1;
        }

        return new Admission(() => Leave(prefix));
    }

    /// <summary>Counts a refusal against the name and the origin and answers at once: the entry that was not even checked.</summary>
    internal void Counted(string name, IPAddress? origin) => _ = Register(name, origin);

    /// <summary>Counts the refusal against the name and the origin and waits the delay the greater counter asks, holding the place the entry was admitted to.</summary>
    internal async Task RefusedAsync(string name, IPAddress? origin, CancellationToken cancellationToken)
    {
        var delay = Register(name, origin);
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, clock, cancellationToken).ConfigureAwait(false);
        }
    }


    /// <summary>An accepted entry zeroes the counters of its name and its origin.</summary>
    internal void Accepted(string name, IPAddress? origin)
    {
        lock (gate)
        {
            _ = typed.Remove(NameKey(name));
            _ = origins.Remove(OriginKey(origin));
        }
    }

    /// <summary>The delay the n-th refusal in a row waits: none for the first, then 1 s doubling to the ceiling.</summary>
    internal static TimeSpan DelayOf(int refusals)
    {
        var doublings = refusals - FirstDelayed;
        if (doublings < 0)
        {
            return TimeSpan.Zero;
        }

        return doublings >= Doublings ? Ceiling : TimeSpan.FromTicks(Math.Min(First.Ticks << doublings, Ceiling.Ticks));
    }

    /// <summary>The network prefix of the address that is kept: /24 for IPv4 and /48 for IPv6, never the whole address.</summary>
    internal static string OriginKey(IPAddress? origin)
    {
        if (origin is null)
        {
            return Unknown;
        }

        var address = origin.IsIPv4MappedToIPv6 ? origin.MapToIPv4() : origin;
        var bytes = address.GetAddressBytes();
        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? $"v4:{Convert.ToHexString(bytes, 0, Ipv4PrefixBytes)}"
            : $"v6:{Convert.ToHexString(bytes, 0, Ipv6PrefixBytes)}";
    }

    private string NameKey(string name) => Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(name)));

    private TimeSpan Register(string name, IPAddress? origin)
    {
        var now = clock.GetUtcNow();
        lock (gate)
        {
            Forget(now);
            var byName = Count(typed, NameKey(name), now);
            var byOrigin = Count(origins, OriginKey(origin), now);
            return DelayOf(Math.Max(byName, byOrigin));
        }
    }

    private static int Count(IDictionary<string, Streak> counters, string counted, DateTimeOffset now)
    {
        var known = counters.TryGetValue(counted, out var streak) && now - streak.Last < Forgetting;
        if (!known && counters.Count >= MaxCounters)
        {
            return 1;
        }

        var refusals = (known ? streak.Refusals : 0) + 1;
        counters[counted] = new Streak(refusals, now);
        return refusals;
    }

    private void Forget(DateTimeOffset now)
    {
        if (now - swept < SweepEvery)
        {
            return;
        }

        swept = now;
        Forget(typed, now);
        Forget(origins, now);
    }

    private static void Forget(IDictionary<string, Streak> counters, DateTimeOffset now)
    {
        foreach (var old in counters.Where(entry => now - entry.Value.Last >= Forgetting).Select(entry => entry.Key).ToList())
        {
            _ = counters.Remove(old);
        }
    }

    private void Leave(string prefix)
    {
        lock (gate)
        {
            waiting--;
            if (waitingByOrigin[prefix] <= 1)
            {
                _ = waitingByOrigin.Remove(prefix);
                return;
            }

            waitingByOrigin[prefix]--;
        }
    }

    private readonly record struct Streak(int Refusals, DateTimeOffset Last);

    /// <summary>The place of an entry in progress; disposing it, once, gives the place back.</summary>
    internal sealed class Admission : IDisposable
    {
        private readonly Action release;
        private int released;

        internal Admission(Action release)
        {
            this.release = release;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
            {
                release();
            }
        }
    }
}
