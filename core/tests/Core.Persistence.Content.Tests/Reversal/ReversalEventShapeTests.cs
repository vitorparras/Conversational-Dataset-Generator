using System.Globalization;
using Core.Ingestion.Contracts.Reversal;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Reversal;

/// <summary>
/// What each event of the reversal points to (issue 103, eventos-da-reversao.md): the kind and the identifier of its item, its reason and a reference that carries a marker, a scope or
/// an identifier and never text; a request and its authorization that never change, and a refusal that is an event of its own and leaves no row.
/// </summary>
[Trait("Category", "Database")]
public sealed class ReversalEventShapeTests
{
    private const string Stored =
        "SELECT item_kind || '|' || item_id || '|' || step || '|' || reason || '|' || coalesce(reference, '') || '|' || actor_person_id FROM public.custody_event WHERE sequence = @sequence";

    private const string AboutAPerson = "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, reference) "
        + "VALUES (@actor, false, 'person', @item, @step, @reason, NULLIF(@reference, ''))";

    private const string RefusalOfAnItem = "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, reference) "
        + "VALUES (1, false, @kind, 1, 'reversal_request_refused', @reason, @reference)";

    private const string OfAKind = "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, reference) "
        + "VALUES (1, false, @kind, 41, @step, @reason, NULLIF(@reference, ''))";

    private const string CountRequests = "SELECT count(*) FROM content.reversal_request";

    private const string CountAuthorizations = "SELECT count(*) FROM content.reversal_authorization";

    private static readonly Dictionary<string, (CustodyEvent Event, string Stored)> Steps = new(StringComparer.Ordinal)
    {
        ["pedido"] = (CustodyEvent.ReversalRequested(1, 41, ReversalRows.Marker), "reversal_request|41|reversal_requested|administrative_act|PESSOA_48291307|1"),
        ["recusa-funcao"] =
            (CustodyEvent.ReversalRequestRefused(1, 118, ReversalRows.Marker, CustodyReason.ReversalFunction), "item_text|118|reversal_request_refused|reversal_function|PESSOA_48291307|1"),
        ["recusa-dominio"] = (CustodyEvent.ReversalRequestRefusedByScope(1, 1, ReversalScope.Domain), "domain|1|reversal_request_refused|reversal_scope|dominio|1"),
        ["recusa-varios-itens"] = (CustodyEvent.ReversalRequestRefusedByScope(1, 1, ReversalScope.ManyItems), "domain|1|reversal_request_refused|reversal_scope|varios-itens|1"),
        ["recusa-varios-marcadores"] =
            (CustodyEvent.ReversalRequestRefusedByScope(1, 2, ReversalScope.ManyMarkers), "domain|2|reversal_request_refused|reversal_scope|varios-marcadores|1"),
        ["recusa-fora-do-item"] =
            (CustodyEvent.ReversalRequestRefused(1, 118, "LUGAR_00000001", CustodyReason.ReversalScope), "item_text|118|reversal_request_refused|reversal_scope|LUGAR_00000001|1"),
        ["leitura"] = (CustodyEvent.Of(CustodyAuthor.Person(3), new CustodyItem(CustodyItemKind.ReversalRequest, 41), CustodyStep.ReversalRequestRead, CustodyReason.AdministrativeAct),
            "reversal_request|41|reversal_request_read|administrative_act||3"),
        ["autorizacao"] = (CustodyEvent.ReversalAuthorized(3, 9, 41), "reversal_authorization|9|reversal_authorized|administrative_act|41|3"),
        ["autorizacao-propria"] =
            (CustodyEvent.ReversalAuthorizationRefused(1, 41, CustodyReason.ReversalSelf), "reversal_request|41|reversal_authorization_refused|reversal_self||1"),
        ["autorizacao-sem-funcao"] =
            (CustodyEvent.ReversalAuthorizationRefused(1, 41, CustodyReason.ReversalFunction), "reversal_request|41|reversal_authorization_refused|reversal_function||1"),
        ["declinio"] = (CustodyEvent.ReversalDeclined(3, 41), "reversal_request|41|reversal_declined|administrative_act||3"),
        ["revelacao"] = (CustodyEvent.ReversalRevealed(3, 12, 9), "reversal_revelation|12|reversal_revealed|administrative_act|9|3"),
        ["negativa-vencida"] = (CustodyEvent.ReversalDenied(3, 9, CustodyReason.ReversalExpired), "reversal_authorization|9|reversal_denied|reversal_expired||3"),
        ["negativa-usada"] = (CustodyEvent.ReversalDenied(3, 9, CustodyReason.ReversalUsed), "reversal_authorization|9|reversal_denied|reversal_used||3"),
        ["negativa-esvaziada"] = (CustodyEvent.ReversalDenied(3, 9, CustodyReason.ReversalEmptied), "reversal_authorization|9|reversal_denied|reversal_emptied||3"),
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> StepCases => [.. Steps.Keys];

    [DatabaseTheory]
    [MemberData(nameof(StepCases))]
    public async Task EachReversalStepPointsToItsListedKindAndReference(string step)
    {
        await using var world = await ContentWorld.CreateAsync();
        var (custodyEvent, stored) = Steps[step];

        var appended = await new CustodyChain(world.DataSource).AppendAsync(custodyEvent, Token);

        Assert.Equal(CustodyOutcome.Appended, appended.Outcome);
        Assert.Equal(stored, await world.ScalarAsync(Stored, ("sequence", Assert.NotNull(appended.Sequence))));
        var parts = stored.Split('|');
        var aboutAPerson = await Assert.ThrowsAsync<PostgresException>(() => world.ScalarAsync(
            AboutAPerson, ("actor", long.Parse(parts[5], CultureInfo.InvariantCulture)), ("item", long.Parse(parts[1], CultureInfo.InvariantCulture)), ("step", parts[2]),
            ("reason", parts[3]), ("reference", parts[4])));
        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_reversal_shape"), (aboutAPerson.SqlState, aboutAPerson.ConstraintName));
    }

    [DatabaseTheory]
    [InlineData("item_text", "reversal_function", "PESSOA 48291307")]
    [InlineData("item_text", "reversal_function", "PESSOA_48291307 zqx-nome")]
    [InlineData("item_text", "reversal_scope", "zqx-porquê")]
    [InlineData("item_text", "reversal_scope", "PESSOA_4829130")]
    [InlineData("domain", "reversal_scope", "varios itens")]
    [InlineData("domain", "reversal_scope", "zqx-uso")]
    public async Task RefusalReferenceCarriesNoText(string kind, string reason, string reference)
    {
        await using var world = await ContentWorld.CreateAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.ScalarAsync(RefusalOfAnItem, ("kind", kind), ("reason", reason), ("reference", reference)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    [DatabaseTheory]
    [InlineData("reversal_request", "reversal_requested", "PESSOA_48291307")]
    [InlineData("reversal_request", "reversal_request_read", "")]
    [InlineData("reversal_authorization", "reversal_authorized", "41")]
    [InlineData("reversal_request", "reversal_declined", "")]
    [InlineData("reversal_revelation", "reversal_revealed", "9")]
    public async Task ActOfTheReversalIsRecordedOnlyAsAnAdministrativeAct(string kind, string step, string reference)
    {
        await using var world = await ContentWorld.CreateAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.ScalarAsync(
            OfAKind, ("kind", kind), ("step", step), ("reason", "content_read"), ("reference", reference)));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_reversal_shape"), (refused.SqlState, refused.ConstraintName));
        _ = await world.ScalarAsync(OfAKind, ("kind", kind), ("step", step), ("reason", "administrative_act"), ("reference", reference));
    }

    [DatabaseTheory]
    [InlineData("UPDATE content.reversal_request SET marker = 'PESSOA_00000001'")]
    [InlineData("UPDATE content.reversal_request SET term_days = 1")]
    [InlineData("UPDATE content.reversal_request SET justification = '\\x01'::bytea")]
    [InlineData("UPDATE content.reversal_request SET requested_by = 3, dek_epoch = dek_epoch")]
    [InlineData("DELETE FROM content.reversal_request")]
    [InlineData("TRUNCATE content.reversal_request CASCADE")]
    [InlineData("UPDATE content.reversal_authorization SET valid_until = valid_until + interval '1 day'")]
    [InlineData("DELETE FROM content.reversal_authorization")]
    [InlineData("TRUNCATE content.reversal_authorization CASCADE")]
    public async Task RequestRowsCannotBeChanged(string statement)
    {
        await using var world = await ContentWorld.CreateAsync();
        var request = await ReversalRows.RequestAsync(world, 1, 1);
        _ = await ReversalRows.AuthorizeAsync(world, (1, request), 3, "30 days");

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, statement));

        Assert.Equal((CustodyStatements.BatchSqlState, 1L, 1L), (refused.SqlState, await CountAsync(world, CountRequests), await CountAsync(world, CountAuthorizations)));
    }

    [DatabaseFact]
    public async Task RequestKeyIsWrappedAgainOnlyWithTheEventOfTheRotationOrOfTheRewrap()
    {
        await using var world = await ContentWorld.CreateAsync();
        var request = await ReversalRows.RequestAsync(world, 1, 1);

        var refused = await Assert.ThrowsAsync<PostgresException>(
            () => world.InDomainAsync(1, "UPDATE content.reversal_request SET dek_wrapped = '\\x01'::bytea WHERE id = @id", ("id", request)));

        Assert.Equal(CustodyStatements.WithoutEventSqlState, refused.SqlState);
    }

    [DatabaseTheory]
    [InlineData("INSERT INTO content.reversal_request (domain_id, item_id, marker, requested_by, term_days, justification, dek_epoch, dek_wrapped) "
        + "VALUES (1, 118, 'PESSOA_48291307', 1, 30, '\\x00'::bytea, 1, '\\x00'::bytea)")]
    [InlineData("INSERT INTO content.reversal_authorization (domain_id, request_id, authorized_by, valid_until) "
        + "SELECT 1, id, 3, now() + interval '30 days' FROM content.reversal_request")]
    public async Task RowWithoutItsEventIsRefused(string statement)
    {
        await using var world = await ContentWorld.CreateAsync();
        _ = await ReversalRows.RequestAsync(world, 1, 1);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, statement));

        Assert.Equal(CustodyStatements.WithoutEventSqlState, refused.SqlState);
    }

    [DatabaseFact]
    public async Task RefusalIsItsOwnEventAndCannotBeRemovedOrChanged()
    {
        await using var world = await ContentWorld.CreateAsync();
        var plain = Assert.Throws<ArgumentException>(() => CustodyEvent.Of(
            CustodyAuthor.Person(1), new CustodyItem(CustodyItemKind.ItemText, 118), CustodyStep.ReversalRequestRefused, CustodyReason.ReversalFunction));

        var appended = await new CustodyChain(world.DataSource).AppendAsync(CustodyEvent.ReversalRequestRefused(1, 118, ReversalRows.Marker, CustodyReason.ReversalFunction), Token);

        var sequence = Assert.NotNull(appended.Sequence);
        Assert.Equal(("step", CustodyOutcome.Appended, 0L), (plain.ParamName, appended.Outcome, await CountAsync(world, CountRequests)));
        await world.Database.RunAsOwnerAsync("GRANT UPDATE, DELETE ON public.custody_event TO owner");
        foreach (var change in new[] { "UPDATE public.custody_event SET reference = 'PESSOA_00000002' WHERE sequence = @sequence", "DELETE FROM public.custody_event WHERE sequence = @sequence" })
        {
            var refused = await Assert.ThrowsAsync<PostgresException>(() => world.ScalarAsync(change, ("sequence", sequence)));
            Assert.Equal(CustodyStatements.ImmutableSqlState, refused.SqlState);
        }

        Assert.Equal("item_text|118|reversal_request_refused|reversal_function|PESSOA_48291307|1", await world.ScalarAsync(Stored, ("sequence", sequence)));
    }

    private static async Task<long> CountAsync(ContentWorld world, string statement) => (long)(await world.OwnerInDomainAsync(1, statement))!;
}
