using Microsoft.Extensions.Logging;

namespace Core.Platform.Startup;

/// <summary>Runs the startup checks one by one, in the order given, and logs each result.</summary>
public static partial class StartupRunner
{
    /// <summary>
    /// Runs every check, even after one fails, so the operator sees every missing item at once.
    /// The order is the caller's, written once in the host's composition.
    /// </summary>
    public static async Task<StartupReport> RunAsync(
        IReadOnlyList<IStartupCheck> checks,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(logger);

        var results = new List<CheckResult>(checks.Count);
        foreach (var check in checks)
        {
            var result = await RunOneAsync(check, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            if (result.Status == CheckStatus.Passed)
            {
                CheckPassed(logger, result.Name, result.Message);
            }
            else
            {
                CheckFailed(logger, result.Name, result.Message, result.ExpectedSource);
            }
        }

        return new StartupReport(results);
    }

    private static async Task<CheckResult> RunOneAsync(IStartupCheck check, CancellationToken cancellationToken)
    {
        try
        {
            return await check.RunAsync(cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Any exception from a check is that check's failure, reported by name.
        catch (Exception exception) when (exception is not OperationCanceledException)
#pragma warning restore CA1031
        {
            // The exception message is free text and stays out; its type says enough to start.
            return CheckResult.Fail(
                check.Name,
                $"a conferência lançou {exception.GetType().FullName}",
                "a própria conferência");
        }
    }

    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Conferência {Name}: aprovada ({Detail})")]
    private static partial void CheckPassed(ILogger logger, string name, string detail);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Error, Message = "Conferência {Name}: reprovada ({Detail}); deveria vir de: {ExpectedSource}")]
    private static partial void CheckFailed(ILogger logger, string name, string detail, string expectedSource);
}
