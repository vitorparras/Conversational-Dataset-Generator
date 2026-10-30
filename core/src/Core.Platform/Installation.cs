namespace Core.Platform;

/// <summary>The installation as the operator sees it: its kind, and a name that says so when it is a demonstration.</summary>
public sealed record Installation(InstallationKind Kind)
{
    private const string Product = "Conversational Dataset Generator";

    /// <summary>The product's name, followed by the mark of a demonstration when it is one.</summary>
    public string Name => Kind == InstallationKind.Demonstration ? $"{Product} (DEMONSTRAÇÃO)" : Product;
}
