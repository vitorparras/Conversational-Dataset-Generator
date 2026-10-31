using System.Runtime.CompilerServices;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>A theory that needs the database container: skipped with <see cref="DockerAvailability.SkipReason"/> when Docker is out of reach.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class DatabaseTheoryAttribute : TheoryAttribute
{
    public DatabaseTheoryAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        Skip = DockerAvailability.SkipReason;
        SkipUnless = nameof(DockerAvailability.ShouldRun);
        SkipType = typeof(DockerAvailability);
    }
}
