using Core.Platform.Startup;

namespace Core.Host;

/// <summary>
/// Writes what the comparison mode found, one line for each difference with the counts and the steps, never a value: the exit code is the verdict.
/// A log file the server's fall cut short is one warning line with its name and does not change the verdict.
/// </summary>
internal static class AuditCompareReport
{
    /// <summary>
    /// Success when the layers agree or differ only as already known; <see cref="ExitCode.AuditDifferent"/> when there is a difference that
    /// is new; <see cref="ExitCode.ServingFailed"/> when the comparison could not run.
    /// </summary>
    internal static ExitCode Write(TextWriter output, TextWriter errors, AuditCompareOutcome outcome)
    {
        if (outcome.Failure is not null)
        {
            errors.WriteLine($"A comparação não pôde ser feita: uma falha a encerrou ({outcome.Failure}).");
            return ExitCode.ServingFailed;
        }

        foreach (var difference in outcome.Differences)
        {
            var known = difference.KnownGap is null ? string.Empty : $" [conhecida: {difference.KnownGap}]";
            output.WriteLine(
                $"diferença: papel={difference.Role} tabela={difference.Table} comandos={string.Join('|', difference.Commands)} "
                + $"visto={difference.Seen} gravado={difference.Recorded} passos={string.Join('|', difference.Steps)}{known}");
        }

        foreach (var file in outcome.CutOffFiles)
        {
            output.WriteLine($"aviso: o arquivo {file} do registro do servidor termina numa linha cortada pela queda do servidor; a linha ficou de fora e o arquivo não deve ser mexido");
        }

        var added = outcome.Differences.Count(difference => difference.KnownGap is null);
        output.WriteLine(
            added == 0
                ? $"comparação: sem diferença nova entre o servidor e a cadeia ({outcome.Differences.Count} conhecida(s))"
                : $"comparação: {added} diferença(s) nova(s) entre o servidor e a cadeia");
        return added == 0 ? ExitCode.Success : ExitCode.AuditDifferent;
    }
}
