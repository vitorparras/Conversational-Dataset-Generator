using Core.Platform.Versions;

namespace Core.Boundary.Http;

/// <summary>The port through which the core asks which versions the language service declares.</summary>
public interface IServiceVersionReader
{
    /// <summary>Asks once; a version the service did not give, for whatever reason, is absent, never an exception or a version kept from before.</summary>
    /// <exception cref="OperationCanceledException">The caller cancelled; a time limit that passes is absent versions instead.</exception>
    Task<ServiceVersions> ReadAsync(CancellationToken cancellationToken);
}
