using Core.Platform.Versions;

namespace Core.Boundary.Http;

/// <summary>The port through which the core asks which model version the language service runs.</summary>
public interface IModelVersionReader
{
    /// <summary>Asks once; a version the service did not give, for whatever reason, is absent, never an exception or a version kept from before.</summary>
    /// <exception cref="OperationCanceledException">The caller cancelled; a time limit that passes is an absent version instead.</exception>
    Task<VersionValue> ReadAsync(CancellationToken cancellationToken);
}
