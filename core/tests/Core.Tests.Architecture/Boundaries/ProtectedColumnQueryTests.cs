using Core.Tests.Architecture.CodeRules;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>No statement filters or orders by a protected column, because the database holds ciphertext there and would answer over bytes that mean nothing.</summary>
public sealed class ProtectedColumnQueryTests
{
    private static readonly HashSet<string> Columns = new(["raw_text", "sanitized_text", "entries"], StringComparer.Ordinal);

    [Fact]
    public void ProductionFiltersAndOrdersByNoProtectedColumn()
    {
        var files = SourceTree.ProductionFiles();

        var violations = ProtectedColumnQueryRules.Check(files, ProtectedColumnQueryRules.ColumnsDeclaredIn(files));

        Assert.Empty(violations);
    }

    [Fact]
    public void TheRuleReadsEveryProtectedColumnTheCodeDeclares()
    {
        var declared = ProtectedColumnQueryRules.ColumnsDeclaredIn(SourceTree.ProductionFiles());

        Assert.Equal(Columns.Order(StringComparer.Ordinal), declared.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EveryWayOfFilteringOrderingGroupingAndJoiningByAProtectedColumnIsRefused()
    {
        var fixture = SourceTree.Fixture(Path.Combine("protected-columns", "FiltersOrOrders.cs"));

        var lines = ProtectedColumnQueryRules.Check([fixture], Columns).Select(violation => violation.Line).Order();

        Assert.Equal([5, 7, 9, 11, 13, 15, 21, 23, 25, 27, 29, 31, 33, 35, 37, 39], lines);
    }

    [Fact]
    public void WritingReturningAndSelectingAProtectedColumnIsNotFilteringByIt()
    {
        var fixture = SourceTree.Fixture(Path.Combine("protected-columns", "NeverFiltersOrOrders.cs"));

        var violations = ProtectedColumnQueryRules.Check([fixture], Columns);

        Assert.Empty(violations);
    }

    [Fact]
    public void ViolationNamesTheColumn()
    {
        var fixture = SourceTree.Fixture(Path.Combine("protected-columns", "FiltersOrOrders.cs"));

        var message = ProtectedColumnQueryRules.Check([fixture], Columns).First().Message;

        Assert.Contains("raw_text", message, StringComparison.Ordinal);
    }
}
