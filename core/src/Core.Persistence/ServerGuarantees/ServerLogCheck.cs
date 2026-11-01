using Core.Platform.Startup;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Checks that the folder named for the server's audit log is one the application can read and that holds a log file: without it the
/// comparison of what the server saw with what the application wrote has nothing to read, and the diferença would go unseen.
/// </summary>
public sealed class ServerLogCheck(string directory) : IStartupCheck
{
    private const string Origin = "pasta nomeada por Audit:ServerLogDirectory";

    /// <inheritdoc />
    public string Name => "ServerLog";

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken) => Task.FromResult(Judged());

    private CheckResult Judged()
    {
        try
        {
            var file = new DirectoryInfo(directory).EnumerateFiles("*.csv").FirstOrDefault();
            if (file is null)
            {
                return CheckResult.Failed(Name, "a pasta do registro do servidor não tem arquivo .csv: o servidor não escreve o registro nela", Origin);
            }

            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return CheckResult.Passed(Name, "registro do servidor legível na pasta declarada", Origin);
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException)
        {
            return CheckResult.Failed(Name, $"a pasta do registro do servidor não pode ser lida pelo processo: {problem.GetType().Name}", Origin);
        }
    }
}
