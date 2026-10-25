namespace Core.Persistence.Migrations;

/// <summary>What a run did and, when it did not finish, where and why; it carries digests and step names, never the connection.</summary>
public sealed record MigrationResult
{
    /// <summary>How the run ended.</summary>
    public required MigrationOutcome Outcome { get; init; }

    /// <summary>The version of the last step in the registry when the run ended or was refused, or 0.</summary>
    public int Found { get; init; }

    /// <summary>The version of the last step this build carries.</summary>
    public int Expected { get; init; }

    /// <summary>The step that diverged or failed; null otherwise.</summary>
    public string? Step { get; init; }

    /// <summary>The digest the registry holds for the divergent step; null otherwise.</summary>
    public string? Registered { get; init; }

    /// <summary>The digest the divergent step has now; null otherwise.</summary>
    public string? Current { get; init; }

    /// <summary>The SQLSTATE the server answered with when a step or the connection failed, if it gave one.</summary>
    public string? SqlState { get; init; }

    /// <summary>The steps this run applied and registered, in order, also when a later one failed.</summary>
    public IReadOnlyList<string> Applied { get; init; } = [];
}
