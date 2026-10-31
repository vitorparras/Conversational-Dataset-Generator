namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// What the server answers, to the application's role, about the settings that decide what the audit log holds. A null is a setting the server does not define.
/// </summary>
/// <param name="Log">pgaudit.log: the classes of statement the audit extension logs.</param>
/// <param name="LogParameter">pgaudit.log_parameter: whether the audit line carries the parameters of the statement.</param>
/// <param name="ParameterMaxLength">log_parameter_max_length: how much of the parameters the statement log keeps.</param>
/// <param name="ParameterMaxLengthOnError">log_parameter_max_length_on_error: how much of the parameters the log of an error keeps.</param>
/// <param name="MinMessages">log_min_messages: the lowest level the server writes. An error's DETAIL names the offending key or the whole failing row.</param>
internal sealed record AuditSettings(string? Log, string? LogParameter, string? ParameterMaxLength, string? ParameterMaxLengthOnError, string? MinMessages)
{
    private const string Off = "off";
    private const string Zero = "0";

    /// <summary>The classes of statement the application's role must have in the log; without both, an operation of its own goes by unseen.</summary>
    private static readonly string[] RequiredClasses = ["read", "write"];

    /// <summary>The audit lines are written at LOG, which ranks above ERROR for this setting: from here up, the audit stays and the error text goes.</summary>
    private static readonly string[] LevelsWithoutErrors = ["log", "fatal", "panic"];

    /// <summary>What is wrong with the settings, one entry for each, each saying what to set; empty when they hold.</summary>
    internal IReadOnlyList<string> Gaps() =>
        [.. new[]
        {
            ClassesGap(),
            GapUnlessEqual(LogParameter, Off, "pgaudit.log_parameter"),
            GapUnlessEqual(ParameterMaxLength, Zero, "log_parameter_max_length"),
            GapUnlessEqual(ParameterMaxLengthOnError, Zero, "log_parameter_max_length_on_error"),
            ErrorsGap(),
        }.OfType<string>()];

    private static string? GapUnlessEqual(string? value, string expected, string setting) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase)
            ? null
            : $"{setting} = '{value ?? "indefinido"}' deixa parâmetro no log do servidor: defina {setting} = {expected}";

    private string? ClassesGap()
    {
        var missing = RequiredClasses.Where(auditedClass => !Covers(auditedClass)).ToArray();
        return missing.Length == 0
            ? null
            : $"pgaudit.log = '{Log}' não registra {string.Join(" e ", missing)} do papel da aplicação: defina pgaudit.log com read e write para ele";
    }

    private string? ErrorsGap() =>
        LevelsWithoutErrors.Contains(MinMessages, StringComparer.OrdinalIgnoreCase)
            ? null
            : $"log_min_messages = '{MinMessages ?? "indefinido"}' deixa no log do servidor o detalhe de um erro, que cita a chave ou a linha inteira: defina log_min_messages = log";

    /// <summary>pgaudit reads the list in order: a name adds its class, "all" adds every one, and a minus sign takes the class back.</summary>
    private bool Covers(string auditedClass)
    {
        var covered = false;
        foreach (var token in (Log ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var removes = token.StartsWith('-');
            var name = removes ? token[1..] : token;
            if (name.Equals(auditedClass, StringComparison.OrdinalIgnoreCase) || name.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                covered = !removes;
            }
        }

        return covered;
    }
}
