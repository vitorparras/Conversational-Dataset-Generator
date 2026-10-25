namespace Core.Host.Tests.Privacy;

/// <summary>The real language service writes to a destination of the log, so a run without it is a failure and never a skip: a destination that cannot be swept must not pass green.</summary>
internal static class RealServiceGuard
{
    /// <summary>The interpreter, or a failed test naming what is missing.</summary>
    public static string Require(string? interpreter)
    {
        if (interpreter is null)
        {
            Assert.Fail("The privacy sweep needs the real language service and uv could not resolve its environment; run 'uv sync --frozen' in language-service.");
        }

        return interpreter;
    }
}
