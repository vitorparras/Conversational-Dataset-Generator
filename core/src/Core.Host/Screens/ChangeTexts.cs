using Core.Host.Settings;

namespace Core.Host.Screens;

/// <summary>The words of the screen of the changes of configuration (decision 0053): the name of each key the screen changes, of each motive, and what each answer tells.</summary>
internal static class ChangeTexts
{
    internal const string SubmittedDone = "enviada";
    internal const string ApprovedDone = "aprovada";
    internal const string RefusedDone = "recusada";

    internal static string? Done(string? done) => done switch
    {
        SubmittedDone => "Alteração enviada. Ela aguarda a aprovação de outra pessoa da Administração técnica.",
        ApprovedDone => "Alteração aprovada. O aplicador a grava no servidor e reinicia o sistema; até lá, vale o valor em vigor.",
        RefusedDone => "Alteração recusada. Nada muda na configuração.",
        _ => null,
    };

    internal static string Key(string key) => key switch
    {
        "Session:IdleTimeout" => "Tempo de inatividade da sessão",
        "Session:AbsoluteTimeout" => "Duração máxima da sessão",
        "Passwords:WorkFactor" => "Custo do cálculo das senhas",
        "Custody:VerificationInterval" => "Intervalo da verificação da cadeia",
        "Audit:CompareInterval" => "Intervalo da comparação da auditoria",
        "LanguageService:StartupTimeoutSeconds" => "Prazo de partida do serviço de linguagem, em segundos",
        "LanguageService:AnalyzeTimeoutSeconds" => "Prazo de cada análise do serviço de linguagem, em segundos",
        _ => key,
    };

    internal static string Motive(string motive) => motive switch
    {
        "hardening" => "Endurecer a proteção",
        "operational_adjustment" => "Ajuste operacional",
        "value_correction" => "Correção de valor errado",
        "obligation" => "Exigência de contrato ou norma",
        "incident_response" => "Resposta a incidente",
        _ => motive,
    };

    internal static string? Refused(ChangeAnswer? answer) => answer?.Outcome switch
    {
        ChangeOutcome.OnlyOnServer => "Essa chave só muda no servidor, no arquivo de configuração.",
        ChangeOutcome.OverriddenOnServer => "Essa chave está definida no servidor por variável de ambiente ou argumento; a alteração pela tela não teria efeito.",
        ChangeOutcome.MotiveUnknown => "Escolha um motivo da lista.",
        ChangeOutcome.WithoutFunction => "Só quem está ativo na Administração técnica propõe alterações.",
        ChangeOutcome.NotRecorded => "A alteração não foi registrada. Tente de novo em instantes; se a falha se repetir, avise quem administra o servidor.",
        _ => null,
    };

    /// <summary>What the start would refuse in the value, naming the key, the value and what it takes; null when the value was not refused.</summary>
    internal static string? Value(ChangeAnswer? answer) =>
        answer is { Outcome: ChangeOutcome.ValueRefused, Problems: { Count: > 0 } problems }
            ? "A partida recusaria este valor: " + string.Join("; ", problems.Select(problem => problem.Describe())) + "."
            : null;

    internal static string? Decision(DecisionOutcome? outcome) => outcome switch
    {
        DecisionOutcome.NotFound => "Essa alteração não existe mais na lista.",
        DecisionOutcome.AlreadyDecided => "Outra pessoa já decidiu essa alteração.",
        DecisionOutcome.OwnSubmission => "Quem propôs a alteração não a decide: outra pessoa da Administração técnica decide.",
        DecisionOutcome.Expired => "O prazo dessa alteração venceu; proponha de novo, se ainda for preciso.",
        DecisionOutcome.WithoutFunction => "Só quem está ativo na Administração técnica decide alterações.",
        DecisionOutcome.NotRecorded => "A decisão não foi registrada. Tente de novo em instantes.",
        _ => null,
    };
}
