using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Core.Persistence.Custody;
using Core.Persistence.Notices;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Tests.People;
using Npgsql;

namespace Core.Persistence.Tests.Notices;

/// <summary>What the tests of the notices share: people made in the database of the class fixture, the notices over the application's data source and a text read as the owner.</summary>
internal static class NoticesDatabase
{
    internal static async Task<PersonNotices> NoticesAsync(this PeopleDatabase database) => new(await database.ApplicationAsync());

    internal static async Task<long> NewPersonAsync(this PeopleDatabase database)
    {
        var registry = await database.RegistryAsync();
        return PeopleDatabase.Id(await registry.CreateAsync(new NewPerson(PeopleDatabase.NewName(), PersonFunctions.Accepted[0]), CustodyAuthor.System, TestContext.Current.CancellationToken));
    }

    [SuppressMessage("Security", "CA2100", Justification = "Each statement is a literal of the calling test, never built from input.")]
    internal static async Task<string> TextAsync(this PeopleDatabase database, string statement)
    {
        await using var connection = await Sql.OpenAsync((await database.ScratchAsync()).OwnerConnectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        return Convert.ToString(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture)!;
    }
}
