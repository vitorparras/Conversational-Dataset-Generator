namespace Core.FixtureModule;

/// <summary>A public type of a module implementation, which another module must not reach.</summary>
public sealed class ModuleService
{
    /// <summary>Something to call, so the reference survives compilation.</summary>
    public static int Answer() => 42;
}
