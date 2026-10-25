using System.Globalization;

namespace Core.Host.Tests;

/// <summary>Decides whether the tests that need Linux run: POSIX signals sent with kill, and /dev/full; elsewhere they skip with the reason.</summary>
internal static class LinuxOnly
{
    public const string SkipReason = "só Linux: sinal POSIX e /dev/full";
    public const string InterruptIgnoredReason =
        "SIGINT chega ignorado a este processo (job em segundo plano de shell não interativo) e o host o herdaria: CTRL+C não testável aqui";

    /// <summary>SIGINT is signal 2, the second bit of the mask the kernel reports.</summary>
    private const ulong InterruptBit = 1UL << 1;

    /// <summary>Read by <see cref="LinuxFactAttribute"/> through its name.</summary>
    public static bool ShouldRun => OperatingSystem.IsLinux();

    /// <summary>Whether this process ignores SIGINT; every process it starts inherits that, and the platform then keeps ignoring it.</summary>
    public static bool InterruptIgnored =>
        (IgnoredSignals() & InterruptBit) != 0;

    private static ulong IgnoredSignals() =>
        ulong.Parse(
            File.ReadLines("/proc/self/status").First(line => line.StartsWith("SigIgn:", StringComparison.Ordinal))["SigIgn:".Length..].Trim(),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture);
}
