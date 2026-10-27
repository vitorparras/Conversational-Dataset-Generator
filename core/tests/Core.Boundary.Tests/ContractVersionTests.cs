using Core.Boundary.Contracts;

namespace Core.Boundary.Tests;

/// <summary>The version the core speaks is the one the language service declares, and no other.</summary>
public sealed class ContractVersionTests
{
    [Fact]
    public void TheCoreSpeaksVersionOne() => Assert.Equal("1", ContractVersion.Current);
}
