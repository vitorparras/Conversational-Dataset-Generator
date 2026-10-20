using System.Runtime.InteropServices;
using Core.Platform.Startup;

namespace Core.Platform.Tests.Startup;

/// <summary>The machine's target keeps the system of the runtime and names the architecture of the machine.</summary>
public sealed class RuntimeTargetTests
{
    [Fact]
    public void EmulatedProcessIsDescribedByTheArchitectureOfTheMachine()
    {
        var target = RuntimeTarget.Describe("win-x64", Architecture.Arm64);

        Assert.Equal("win-arm64", target);
    }

    [Fact]
    public void SystemWithAQualifierKeepsTheQualifier()
    {
        var target = RuntimeTarget.Describe("linux-musl-x64", Architecture.Arm64);

        Assert.Equal("linux-musl-arm64", target);
    }

    [Fact]
    public void ArchitectureIsWrittenAsTheRuntimeIdentifierSpellsIt()
    {
        var target = RuntimeTarget.Describe("linux-x64", Architecture.LoongArch64);

        Assert.Equal("linux-loongarch64", target);
    }

    [Fact]
    public void NativeProcessIsDescribedByItsOwnRuntimeIdentifier()
    {
        var target = RuntimeTarget.Current;

        Assert.Equal(RuntimeInformation.RuntimeIdentifier, target);
    }
}
