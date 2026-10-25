using System.Collections.Frozen;
using System.Runtime.InteropServices;

namespace Core.Platform.Startup;

/// <summary>
/// The target this machine is, written as a runtime identifier: the system the runtime was built for, followed by the
/// architecture of the machine rather than of the process, since the two differ only when the process runs emulated.
/// </summary>
public static class RuntimeTarget
{
    private static readonly FrozenDictionary<Architecture, string> Names = new Dictionary<Architecture, string>
    {
        [Architecture.X64] = "x64",
        [Architecture.X86] = "x86",
        [Architecture.Arm64] = "arm64",
        [Architecture.Arm] = "arm",
        [Architecture.Armv6] = "armv6",
        [Architecture.Wasm] = "wasm",
        [Architecture.S390x] = "s390x",
        [Architecture.LoongArch64] = "loongarch64",
        [Architecture.Ppc64le] = "ppc64le",
        [Architecture.RiscV64] = "riscv64",
    }.ToFrozenDictionary();

    /// <summary>The target of the machine the process runs on.</summary>
    public static string Current => Describe(RuntimeInformation.RuntimeIdentifier, RuntimeInformation.OSArchitecture);

    /// <summary>Keeps the system of the runtime identifier and puts the machine's architecture in place of its own.</summary>
    internal static string Describe(string runtimeIdentifier, Architecture machine)
    {
        var separator = runtimeIdentifier.LastIndexOf('-');
        var system = separator < 0 ? runtimeIdentifier : runtimeIdentifier[..separator];
        return $"{system}-{(Names.TryGetValue(machine, out var name) ? name : machine.ToString())}";
    }
}
