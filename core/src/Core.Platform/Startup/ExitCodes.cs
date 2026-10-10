namespace Core.Platform.Startup;

/// <summary>Process exit codes. Zero is reserved for a shutdown that was asked for.</summary>
public static class ExitCodes
{
    /// <summary>The shutdown was requested and completed.</summary>
    public const int Requested = 0;

    /// <summary>The process stopped because of an unexpected failure.</summary>
    public const int Failure = 1;

    /// <summary>The configuration was refused; the process never started.</summary>
    public const int ConfigurationRefused = 2;

    /// <summary>A startup check failed; the process never opened its port.</summary>
    public const int StartupCheckFailed = 3;
}
