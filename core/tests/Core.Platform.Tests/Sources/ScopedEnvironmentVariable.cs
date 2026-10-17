namespace Core.Platform.Tests.Sources;

/// <summary>A process variable that exists only between construction and dispose.</summary>
internal sealed class ScopedEnvironmentVariable : IDisposable
{
    private readonly string name;

    public ScopedEnvironmentVariable(string name, string value)
    {
        this.name = name;
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose() => Environment.SetEnvironmentVariable(name, null);
}
