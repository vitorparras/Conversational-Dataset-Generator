using Core.Boundary.Http;
using Core.Platform.Versions;

namespace Core.Boundary.Fake;

/// <summary>The version reader of a core with no language service to ask, as the fake recognizer has none: it makes no connection and never has a version.</summary>
public sealed class NoServiceVersionReader : IServiceVersionReader
{
    /// <inheritdoc />
    public Task<ServiceVersions> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(ServiceVersions.Absent);
}
