namespace Core.Boundary.Contracts;

/// <summary>The version of the contract the core speaks, the same one the language service declares.</summary>
public static class ContractVersion
{
    /// <summary>The only version the core knows; a different one is refused.</summary>
    public static string Current { get; } = "1";
}
