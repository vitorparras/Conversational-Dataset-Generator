namespace Core.Platform.Startup;

/// <summary>
/// Something the process needs from outside the artifact, checked before the port opens. A check
/// never reaches the network: what a check needs travels with the installation or is declared in
/// the configuration (requirement 022).
/// </summary>
public interface IStartupCheck
{
    /// <summary>The name the check reports under.</summary>
    string Name { get; }

    /// <summary>Checks the item. An exception counts as a failure of this check.</summary>
    Task<CheckResult> RunAsync(CancellationToken cancellationToken);
}
