using Core.Platform.Versions;

namespace Core.Platform.Tests.Versions;

/// <summary>What a build of the probe stamped: the keys it wrote, and the build information read from them.</summary>
internal sealed record ProbeBuild(BuildInfo Info, IReadOnlyList<string> StampedKeys);
