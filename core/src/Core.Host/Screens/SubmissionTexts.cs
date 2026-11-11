using System.Globalization;

namespace Core.Host.Screens;

/// <summary>
/// The words the screen of the submission says for each stable code the service answers (the contract of #75): the refusal, where it points, the field to correct,
/// the warning that let the file pass and the gate that keeps it out. Every text is fixed by the code alone; none carries a word of the material or the name the file came with.
/// </summary>
internal static class SubmissionTexts
{
    /// <summary>The field of the form the file goes in, which the service never names: a form without a file is refused by the screen before it calls anything.</summary>
    internal const string FileField = "file";

    /// <summary>What the screen says when the submission reached the service and no batch could be opened or closed.</summary>
    internal const string NotRecorded =
        "O envio não foi registrado e nenhum lote foi aberto. Envie de novo; se repetir, quem administra o servidor vê a causa no registro do sistema.";

    /// <summary>What the screen says when the sending met another one at the same time: nothing of the file was kept, and sending it again is safe.</summary>
    internal const string Collided =
        "O envio esbarrou em outro que chegou ao mesmo tempo e nada do arquivo foi gravado. Envie o mesmo arquivo de novo; não há nada a limpar antes.";

    private const long Mebibyte = 1_048_576;
    private const long Kibibyte = 1_024;

    /// <summary>The forms of material (decision 0005), with the value the contract sends and the words that tell them apart.</summary>
    internal static IReadOnlyList<ChoiceGroup.Option> Forms { get; } =
        Array.ConvertAll("conversation comment document labeled-sequence".Split(' '), value => new ChoiceGroup.Option(value, FormOf(value)));

    /// <summary>Why the material was turned back, by the code of the barrier or of the conference.</summary>
    internal static string ReasonOf(string code) => code switch
    {
        "too_large" => "O arquivo passa do tamanho máximo aceito. A leitura parou no limite e nada além dele foi lido.",
        "unknown_format" => "Esta versão do sistema não lê o formato escolhido.",
        "format_not_confirmed" => "O começo do arquivo não confirma o formato escolhido. Confira se é um arquivo desse formato, em UTF-8.",
        "unreadable" => "O arquivo não pôde ser lido inteiro: ele está malformado ou incompleto.",
        _ => UnitReasonOf(code),
    };

    /// <summary>Where a refusal points: the unit by its position, counted from one, and the part of it; the material as a whole when it names neither.</summary>
    internal static string WhereOf(int? unit, string? field) => (unit, field) switch
    {
        (null, null) => "No material inteiro",
        (null, { } part) => $"No campo {PartOf(part)}",
        ({ } position, null) => $"Na unidade {Number(position + 1)}",
        ({ } position, { } part) => $"Na unidade {Number(position + 1)}, campo {PartOf(part)}",
    };

    /// <summary>What the person corrects in a field the service or the screen refused, by the field and the code; never the value typed.</summary>
    internal static string FieldErrorOf(string field, string code) => (field, code) switch
    {
        ("domain", _) => "Escolha o domínio do material.",
        (FileField, _) => "Escolha o arquivo do material.",
        ("declared_form", _) => "Escolha a forma do material.",
        ("source_format", _) => "Escolha o formato do arquivo.",
        ("condition", "unknown") => "Essa condição não está na lista desta instalação. Escolha uma das condições abaixo.",
        ("condition", _) => "Escolha a condição sob a qual o material entra.",
        ("origin", _) => OriginErrorOf(code),
        _ => "Confira este campo.",
    };

    /// <summary>What the service noticed and let pass, by its code.</summary>
    internal static string WarningOf(string code) =>
        code == "name_with_path"
            ? "O nome do arquivo trazia um caminho de pastas. Ele foi descartado, como todo nome de arquivo, e o fato ficou registrado no lote. O envio não foi recusado por isso."
            : "O envio seguiu, com um aviso registrado no lote.";

    /// <summary>Why the ingestion takes nothing in now (#584), by the code of what is missing.</summary>
    internal static string BlockedOf(string code) => code switch
    {
        "recovery_not_rehearsed" => "A entrada de material está fechada: a recuperação da cópia de segurança ainda não foi ensaiada nesta instalação.",
        "recovery_rehearsal_stale" => "A entrada de material está fechada: o último ensaio de recuperação da cópia de segurança passou do prazo.",
        _ => "A entrada de material está fechada nesta instalação.",
    };

    /// <summary>The most bytes a file may have, in the unit a person reads.</summary>
    internal static string SizeOf(long bytes) => bytes switch
    {
        >= Mebibyte => $"{Decimal(bytes / (double)Mebibyte)} MiB",
        >= Kibibyte => $"{Decimal(bytes / (double)Kibibyte)} KiB",
        _ => $"{Number(bytes)} bytes",
    };

    /// <summary>A number with one decimal at most and the comma a person here reads; the host runs with the invariant culture alone.</summary>
    private static string Decimal(double value) => value.ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string FormOf(string value) => value switch
    {
        "conversation" => "Conversa: mensagens, cada uma com autor e instante",
        "comment" => "Comentário: uma manifestação de um autor, sem conversa",
        "document" => "Documento: texto corrido, sem autor por parte",
        _ => "Sequência anotada: texto com a anotação em trechos",
    };

    /// <summary>The refusals of the conference about what the units bring.</summary>
    private static string UnitReasonOf(string code) => code switch
    {
        "no_units" => "O arquivo não traz nenhuma unidade de material.",
        "field_missing" => "Uma unidade não traz algo que a forma escolhida exige.",
        "field_not_allowed" => "Uma unidade traz algo que a forma escolhida não tem.",
        "conversation_thread" => "A forma escolhida é uma manifestação isolada, mas o material traz uma conversa.",
        "authorship_by_part" => "O material dá autor a uma parte de uma forma cujas partes não têm autor.",
        _ => OrderReasonOf(code),
    };

    /// <summary>The refusals of the conference about the order of the messages and the limits of the passages.</summary>
    private static string OrderReasonOf(string code) => code switch
    {
        "order_not_determined" => "Duas mensagens têm o mesmo instante, e nada no conteúdo diz qual vem antes.",
        "order_incoherent" => "Uma resposta vem antes da mensagem que ela responde, ou as respostas formam um ciclo.",
        "passage_outside_text" => "Um trecho começa antes do texto, termina depois dele ou não tem tamanho.",
        "passage_splits_pair" => "O limite de um trecho cai no meio de um caractere.",
        _ => "O material foi recusado, e o motivo ficou registrado no lote.",
    };

    private static string OriginErrorOf(string code) => code switch
    {
        "conflict" => "Escreva a origem ou marque que ela é desconhecida, não os dois.",
        "too_long" => "A origem passa do tamanho aceito. Diga-a em menos palavras.",
        _ => "Diga de onde o material veio, ou marque que a origem é desconhecida.",
    };

    private static string PartOf(string code) => code switch
    {
        "text" => "texto",
        "author" => "autor",
        "instant" => "instante",
        "replies_to" => "a quem responde",
        "passages" => "trechos",
        _ => "não nomeado",
    };
}
