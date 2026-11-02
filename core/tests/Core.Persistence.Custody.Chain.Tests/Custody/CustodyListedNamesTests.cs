using System.Text.RegularExpressions;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// Item, step and reason are names from lists (requirement 021): the server holds the lists the code declares,
/// so no text of a person or of a material gets into a record nobody removes.
/// </summary>
[Trait("Category", "Database")]
public sealed partial class CustodyListedNamesTests : IClassFixture<CustodyDatabase>
{
    private const string Free = "zqx-texto-livre";

    private readonly CustodyDatabase database;

    public CustodyListedNamesTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task KindsTheDatabaseListsAreExactlyTheKindsOfTheCode() =>
        Assert.Equal(Sorted(Enum.GetValues<CustodyItemKind>().Select(CustodyNames.Of)), await ListedAsync("custody_event_item_kind_listed"));

    [DatabaseFact]
    public async Task StepsTheDatabaseListsAreExactlyTheStepsOfTheCode() =>
        Assert.Equal(Sorted(Enum.GetValues<CustodyStep>().Select(CustodyNames.Of)), await ListedAsync("custody_event_step_listed"));

    [DatabaseFact]
    public async Task ReasonsTheDatabaseListsAreExactlyTheReasonsOfTheCode() =>
        Assert.Equal(Sorted(Enum.GetValues<CustodyReason>().Select(CustodyNames.Of)), await ListedAsync("custody_event_reason_listed"));

    [DatabaseFact]
    public async Task KindOutsideTheListIsRefusedToAnyRole()
    {
        var refused = await database.RefusedToAppAsync(Insert(Free, "person_created", "administrative_act"));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_item_kind_listed"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task StepOutsideTheListIsRefusedToAnyRole()
    {
        var refused = await database.RefusedToAppAsync(Insert("person", Free, "administrative_act"));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_step_listed"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task ReasonOutsideTheListIsRefusedToAnyRole()
    {
        var refused = await database.RefusedToOwnerAsync(Insert("person", "person_created", Free));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "custody_event_reason_listed"), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task ListedNameWithPaddingIsNotTheNameAndIsRefused()
    {
        var refused = await database.RefusedToAppAsync(Insert("person", "person_created ", "administrative_act"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, refused.SqlState);
    }

    private static string Insert(string kind, string step, string reason) =>
        $"INSERT INTO public.custody_event (actor_system, item_kind, item_id, step, reason) VALUES (true, '{kind}', 12001, '{step}', '{reason}')";

    private static string[] Sorted(IEnumerable<string> listed) => [.. listed.Order(StringComparer.Ordinal)];

    private async Task<string[]> ListedAsync(string constraint)
    {
        var definition = await database.TextAsOwnerAsync("SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = @name", ("name", constraint));
        return Sorted(Literal().Matches(definition).Select(match => match.Groups[1].Value));
    }

    [GeneratedRegex("'([^']*)'::text", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Literal();
}
