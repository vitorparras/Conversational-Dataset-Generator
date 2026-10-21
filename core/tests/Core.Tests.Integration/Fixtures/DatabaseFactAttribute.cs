using System.Runtime.CompilerServices;

namespace Core.Tests.Integration.Fixtures;

/// <summary>A test that needs the database container: skipped with <see cref="DockerAvailability.SkipReason"/> when Docker is out of reach.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        Skip = DockerAvailability.SkipReason;
        SkipUnless = nameof(DockerAvailability.ShouldRun);
        SkipType = typeof(DockerAvailability);
    }
}
