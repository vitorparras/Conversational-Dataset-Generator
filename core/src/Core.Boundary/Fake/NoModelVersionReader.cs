using Core.Boundary.Http;
using Core.Platform.Versions;

namespace Core.Boundary.Fake;

/// <summary>The model version reader of a core with no language service to ask, as the fake recognizer has none: it makes no connection and never has a version.</summary>
public sealed class NoModelVersionReader : IModelVersionReader
{
    /// <inheritdoc />
    public Task<VersionValue> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(VersionValue.Absent);
}
