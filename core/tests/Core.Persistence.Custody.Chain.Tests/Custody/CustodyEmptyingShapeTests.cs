using System.Globalization;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The server keeps the shape of an emptying whoever writes the event: a protocol number as reference, one of the two motives,
/// a person who did it, and nothing else carries a reference.
/// </summary>
[Trait("Category", "Database")]
public sealed class CustodyEmptyingShapeTests : IClassFixture<CustodyDatabase>
{
    private const string Shape = "custody_event_emptying_shape";

    private readonly CustodyDatabase database;

    public CustodyEmptyingShapeTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseTheory]
    [InlineData("item_text", "item_emptied", "contract_requirement", "NULL")]
    [InlineData("item_text", "item_emptied", "contract_requirement", "'Maria da Silva'")]
    [InlineData("item_text", "item_emptied", "contract_requirement", "''")]
    [InlineData("item_text", "item_emptied", "author_without_function", "'PROT-1'")]
    [InlineData("item_text", "item_emptied", "content_read", "'PROT-1'")]
    [InlineData("person", "item_emptied", "contract_requirement", "'PROT-1'")]
    [InlineData("item_text", "item_emptying_refused", "contract_requirement", "'PROT-1'")]
    [InlineData("item_text", "item_emptying_refused", "item_already_emptied", "NULL")]
    [InlineData("item_text", "raw_text_read", "content_read", "'PROT-1'")]
    public async Task EventThatBreaksTheShapeOfAnEmptyingIsRefusedToAnyRole(string kind, string step, string reason, string reference)
    {
        var person = await PersonAsync();

        var refused = await database.RefusedToOwnerAsync(Insert(person.ToString(CultureInfo.InvariantCulture), "false", kind, step, reason, reference));

        Assert.Equal((PostgresErrorCodes.CheckViolation, Shape), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseFact]
    public async Task EmptyingSignedByTheSystemIsRefusedToAnyRole()
    {
        var refused = await database.RefusedToOwnerAsync(Insert("NULL", "true", "item_text", "item_emptied", "contract_requirement", "'PROT-1'"));

        Assert.Equal((PostgresErrorCodes.CheckViolation, Shape), (refused.SqlState, refused.ConstraintName));
    }

    [DatabaseTheory]
    [InlineData("a b")]
    [InlineData("a;b")]
    [InlineData("ação")]
    public async Task ReferenceWithAnythingButTheAllowedSignsIsRefusedWhateverTheStep(string reference)
    {
        var refused = await database.RefusedToOwnerAsync(Insert("NULL", "true", "item_text", "item_emptying_refused", "item_already_emptied", $"'{reference}'"));

        Assert.Equal((PostgresErrorCodes.CheckViolation, Shape), (refused.SqlState, refused.ConstraintName));
    }

    private static string Insert(string person, string system, string kind, string step, string reason, string reference) =>
        "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, reference) "
        + $"VALUES ({person}, {system}, '{kind}', 12002, '{step}', '{reason}', {reference})";

    private async Task<long> PersonAsync()
    {
        var registry = await database.RegistryAsync();
        var created = await registry.CreateAsync(new NewPerson($"zqx-pessoa-{Guid.NewGuid():N}", PersonFunctions.Accepted[2]), CustodyAuthor.System, TestContext.Current.CancellationToken);
        return created.PersonId!.Value;
    }
}
