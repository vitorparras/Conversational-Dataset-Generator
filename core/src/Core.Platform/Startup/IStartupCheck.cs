namespace Core.Platform.Startup;

/// <summary>One thing the process needs from outside itself, checked before it opens its port; never through a network beyond the installation's own environment.</summary>
public interface IStartupCheck
{
    /// <summary>The item the check looks at, as the operator reads it.</summary>
    string Name { get; }

    /// <summary>Looks once and reports; a missing item is a result, not an exception.</summary>
    Task<CheckResult> RunAsync(CancellationToken cancellationToken);
}
