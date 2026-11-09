using System.Globalization;
using System.Text;
using System.Text.Json;
using Core.Persistence.Content;
using Core.Persistence.Content.Reading;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// The guarantee of the sealed raw text proved with material that looks real, through the service and not with the synthetic case of phase 2 (issue 79): a full dump of the storage
/// shows no stretch of what was submitted (CA-1), the label in clear says the purpose and the domain the content was sealed for (CA-2), and from an item that was accepted the raw text
/// comes back as it arrived, the access being an event of the chain (CA-5).
/// </summary>
public sealed class SubmissionProofsTests
{
    private const string Items = "SELECT id::text FROM content.item_text ORDER BY id";
    private const string RawReads =
        "SELECT step || ',' || item_kind || ',' || item_id || ',' || reason FROM public.custody_event WHERE step = 'raw_text_read' ORDER BY sequence";

    private static readonly string[] Texts =
    [
        "Olá! Preciso remarcar a consulta de quinta-feira para a semana que vem, se houver horário depois das 16h.",
        "Claro. Tenho terça às 17h30 ou quarta às 9h. A clínica fica na Rua das Acácias, 120, sala 4.",
        "Terça serve. Meu telefone mudou, anote por favor: (11) 98877-6655. E o CPF continua o mesmo do cadastro.",
        "Anotado. Confirmo terça, 17h30, com a doutora Marta. Lembre de trazer os exames de sangue em jejum de oito horas.",
    ];

    [DatabaseFact]
    public async Task Ca1ADumpOfTheStorageShowsNoStretchOfTheMaterialSubmittedNotAsTextNorAsBytes()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(content: Material()));
        var dump = await world.Database.DumpAsync();

        Assert.Equal((SubmissionState.Accepted, (long)Texts.Length), (outcome.State, await world.ItemRowsAsync()));
        Assert.Contains("content.item_text", dump, StringComparison.Ordinal);
        Assert.DoesNotContain(Texts.SelectMany(Stretches), stretch => Shown(dump, stretch));
    }

    [DatabaseFact]
    public async Task Ca2TheLabelInClearOfTheRawTextSaysThePurposeAndTheDomainItWasSealedFor()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        _ = await world.SubmitAsync(SubmissionWorld.Request(content: Material()));
        _ = await world.SubmitAsync(SubmissionWorld.Request(content: Material()) with { DomainId = SubmissionWorld.OtherDomain });

        var first = await LabelsAsync(world, SubmissionWorld.Domain);
        var other = await LabelsAsync(world, SubmissionWorld.OtherDomain);

        Assert.Equal(Enumerable.Repeat((LabelPurpose.RawText, SubmissionWorld.Domain), Texts.Length), first);
        Assert.Equal(Enumerable.Repeat((LabelPurpose.RawText, SubmissionWorld.OtherDomain), Texts.Length), other);
    }

    [DatabaseFact]
    public async Task Ca5FromTheIdOfAnAcceptedItemTheRawTextComesBackAsItArrivedAndTheAccessIsAnEventOfTheChain()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await SubmissionWorld.CreateAsync();
        _ = await world.SubmitAsync(SubmissionWorld.Request(content: Material()));
        var ids = (await world.StoredAsync(Items)).Select(id => long.Parse(id, CultureInfo.InvariantCulture)).ToList();
        var before = await world.Database.FirstColumnAsOwnerAsync(RawReads);

        var read = new List<string?>();
        foreach (var id in ids)
        {
            var result = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.Person(SubmissionWorld.Person), ContentColumns.RawText, SubmissionWorld.Domain, id), cancellationToken);
            read.Add(result.Outcome == ContentReadOutcome.Read ? result.Text : null);
        }

        Assert.Empty(before);
        Assert.Equal(Texts, read);
        Assert.Equal(ids.Select(id => $"raw_text_read,item_text,{id},content_read"), await world.Database.FirstColumnAsOwnerAsync(RawReads));
    }

    private static string Material() => JsonSerializer.Serialize(new
    {
        formato = SubmissionWorld.Format,
        mensagens = Texts.Select((text, index) => new
        {
            id = $"m{index}",
            remetente = index % 2 == 0 ? "REMETENTE_A" : "REMETENTE_B",
            instante = $"2031-03-04T08:0{index}:00Z",
            texto = text,
        }),
    });

    private static IEnumerable<string> Stretches(string text) =>
        [text, text[..24], text[(text.Length / 2)..((text.Length / 2) + 16)], text[^20..]];

    private static bool Shown(string dump, string stretch) =>
        dump.Contains(stretch, StringComparison.Ordinal) || dump.Contains(Convert.ToHexStringLower(Encoding.UTF8.GetBytes(stretch)), StringComparison.Ordinal);

    private static async Task<IEnumerable<(LabelPurpose Purpose, int Domain)>> LabelsAsync(SubmissionWorld world, int domain)
    {
        var stored = await world.StoredAsync("SELECT encode(raw_text, 'hex') FROM content.item_text ORDER BY id", domain);
        return [.. stored.Select(hex => EnvelopeCipher.LabelOf(Convert.FromHexString(hex))).Select(Parsed)];
    }

    private static (LabelPurpose Purpose, int Domain) Parsed(string text)
    {
        Assert.True(DerivationLabel.TryParse(text, out var label));
        return (label.Purpose, label.DomainId);
    }
}
