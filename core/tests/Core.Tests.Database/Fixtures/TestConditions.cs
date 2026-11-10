using Core.Persistence.Ingestion;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>The conditions of origin the tests of the batches run under, the most restrictive first: synthetic names, since the installation declares its own.</summary>
internal static class TestConditions
{
    internal const string Strictest = "restrita";
    internal const string Middle = "intermediaria";
    internal const string Loosest = "permissiva";

    internal static OriginConditions Known { get; } = OriginConditions.Of([Strictest, Middle, Loosest]);
}
