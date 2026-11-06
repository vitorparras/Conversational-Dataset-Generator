namespace Core.Ingestion;

/// <summary>
/// The refusal of a batch, a result like any other: the form declared, a code, the cause when the reader failed and the findings that say which units and which fields did not hold.
/// It names positions and fields and carries no word of the material (requirement 021), so it can be shown, logged and kept as it is.
/// </summary>
public sealed record Refusal
{
    private const int Shown = 20;

    /// <summary>The refusal of a batch of the form declared.</summary>
    /// <param name="form">The form the batch was declared as.</param>
    /// <param name="code">Why it was refused.</param>
    /// <param name="cause">Why the reader failed, for <see cref="RefusalCode.Unreadable" />; null otherwise.</param>
    /// <param name="findings">Where it did not hold; empty when the refusal is about the batch as a whole.</param>
    public Refusal(MaterialForm form, RefusalCode code, UnreadableReason? cause, IReadOnlyList<Finding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);
        Form = form;
        Code = code;
        Cause = cause;
        Findings = findings;
    }

    /// <summary>The refusal of a batch as a whole, with no unit to point at.</summary>
    /// <param name="form">The form the batch was declared as.</param>
    /// <param name="code">Why it was refused.</param>
    /// <param name="cause">Why the reader failed, for <see cref="RefusalCode.Unreadable" />; null otherwise.</param>
    public static Refusal Whole(MaterialForm form, RefusalCode code, UnreadableReason? cause)
    {
        var none = new List<Finding>();
        return new Refusal(form, code, cause, none);
    }

    /// <summary>The form the batch was declared as.</summary>
    public MaterialForm Form { get; }

    /// <summary>Why it was refused.</summary>
    public RefusalCode Code { get; }

    /// <summary>Why the reader failed; null unless the code is <see cref="RefusalCode.Unreadable" />.</summary>
    public UnreadableReason? Cause { get; }

    /// <summary>Where it did not hold.</summary>
    public IReadOnlyList<Finding> Findings { get; }

    /// <summary>The words that say what was missing and where, for whoever corrects the material outside the system; built from the code, the positions and the fields only.</summary>
    public string Reason
    {
        get
        {
            var shown = string.Join("; ", Findings.Take(Shown).Select(Where));
            var more = Findings.Count > Shown ? $"; e mais {Findings.Count - Shown}" : string.Empty;
            var where = Findings.Count > 0 ? $": {shown}{more}" : string.Empty;
            return $"lote recusado como {FormWords(Form)}: {CodeWords(Code, Cause)}{where}";
        }
    }

    private static string Where(Finding finding) => (finding.Unit, finding.Field) switch
    {
        ({ } unit, { } field) => $"unidade {unit + 1}, campo {FieldWords(field)}",
        ({ } unit, null) => $"unidade {unit + 1}",
        (null, { } field) => $"campo {FieldWords(field)}",
        _ => "o lote todo",
    };

    private static string FormWords(MaterialForm form) => form switch
    {
        MaterialForm.Conversation => "conversa",
        MaterialForm.Comment => "comentário",
        MaterialForm.Document => "documento",
        MaterialForm.LabeledSequence => "sequência rotulada",
        _ => throw new ArgumentOutOfRangeException(nameof(form), form, "The form is not one of the four."),
    };

    private static string FieldWords(MaterialField field) => field switch
    {
        MaterialField.Text => "texto",
        MaterialField.Author => "autor",
        MaterialField.Instant => "instante",
        MaterialField.RepliesTo => "a mensagem respondida",
        MaterialField.Passages => "trechos",
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "The field is not one of the enumeration."),
    };

    private static string CodeWords(RefusalCode code, UnreadableReason? cause) => code switch
    {
        RefusalCode.Unreadable => $"a leitura não foi completa ({CauseWords(cause)}), e leitura parcial não é aceita",
        RefusalCode.NoUnits => "o material não tem nenhuma unidade",
        RefusalCode.FieldMissing => "falta o que a forma exige",
        RefusalCode.FieldNotAllowed => "há o que a forma não admite",
        RefusalCode.OrderNotDetermined => "duas mensagens têm o mesmo instante, e nada no conteúdo decide qual vem antes: a ordem não pode vir da posição no arquivo",
        RefusalCode.PassageOutsideText => "um trecho começa antes do texto, termina depois dele ou não tem extensão",
        RefusalCode.MoreThanOneUnit => "a forma é de uma unidade só e o material tem mais",
        RefusalCode.OrderIncoherent => "a ordem que as respostas dão contradiz os instantes ou a si mesma: uma resposta é anterior à mensagem que responde, ou as respostas formam um ciclo",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "The code is not one of the enumeration."),
    };

    private static string CauseWords(UnreadableReason? cause) => cause switch
    {
        UnreadableReason.Corrupted => "arquivo danificado ou de outro formato",
        UnreadableReason.Truncated => "arquivo cortado antes do fim",
        UnreadableReason.UnsupportedFormat => "formato que esta versão não lê",
        UnreadableReason.OnlyPartlyRead => "só uma parte foi lida",
        _ => "motivo não informado",
    };
}
