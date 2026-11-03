using Core.Platform.Startup;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Checks that the folder named for the server's audit log is one the application can read, and cannot write in, and that holds a log file: without it the
/// comparison of what the server saw with what the application wrote has nothing to read, and the diferença would go unseen; a folder the
/// application can write in is one where it could change what it is compared with.
/// </summary>
public sealed class ServerLogCheck : IStartupCheck
{
    private const string Origin = "pasta nomeada por Audit:ServerLogDirectory";
    private const string Adjustment = "tire a escrita do usuário da aplicação nela (por exemplo, dono e grupo do servidor e modo 0750)";

    private readonly string directory;

    /// <summary>The check reads the folder the configuration named.</summary>
    public ServerLogCheck(string directory)
    {
        this.directory = directory;
    }

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
            if (WritableFolder.Allows(directory))
            {
                return CheckResult.Failed(
                    Name, $"a pasta {directory} do registro do servidor é gravável pelo processo: a aplicação só lê esse registro; {Adjustment}", Origin);
            }

            return CheckResult.Passed(Name, "registro do servidor legível na pasta declarada", Origin);
        }
        catch (Exception problem) when (problem is IOException or UnauthorizedAccessException)
        {
            return CheckResult.Failed(Name, $"a pasta do registro do servidor não pode ser lida pelo processo: {problem.GetType().Name}", Origin);
        }
    }
}
