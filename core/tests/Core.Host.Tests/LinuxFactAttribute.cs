using System.Runtime.CompilerServices;

namespace Core.Host.Tests;

/// <summary>A test that needs Linux: skipped with <see cref="LinuxOnly.SkipReason"/> on any other system.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class LinuxFactAttribute : FactAttribute
{
    public LinuxFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        Skip = LinuxOnly.SkipReason;
        SkipUnless = nameof(LinuxOnly.ShouldRun);
        SkipType = typeof(LinuxOnly);
    }
}
