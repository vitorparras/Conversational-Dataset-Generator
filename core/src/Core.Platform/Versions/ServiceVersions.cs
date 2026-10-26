namespace Core.Platform.Versions;

/// <summary>
/// The three versions the language service declares about itself. Each is absent on its own account: a service that
/// does not answer has none, and one that answers with a blank field lacks only that one.
/// </summary>
/// <param name="Contract">The version of the contract the service speaks.</param>
/// <param name="Service">The version of the service.</param>
/// <param name="Model">The version of the model behind the service.</param>
public sealed record ServiceVersions(VersionValue Contract, VersionValue Service, VersionValue Model)
{
    /// <summary>The versions of a service that gave none.</summary>
    public static ServiceVersions Absent { get; } = new(VersionValue.Absent, VersionValue.Absent, VersionValue.Absent);
}
