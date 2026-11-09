using System.Net;
using System.Text.Json;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The first password the administration sets when it registers a person (#599, CA-1): it is hashed and kept as the derivation, and the text of it is not in the answer, in any row of the registry,
/// of the record of the changes, of the chain or of the credentials, nor in the log of the host.
/// </summary>
[Trait("Category", "Database")]
public sealed class PeopleInitialPasswordProcessTests
{
    private const string Initial = "ZQX-initial-secret-6190";

    [DatabaseFact]
    public async Task TheFirstPasswordIsInNoAnswerNoRowAndNoLogAndOpensTheNewPerson()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup { Function = PersonFunctions.Administration, WithLogFile = true }, cancellationToken);
        var name = $"zqx-nova-{Guid.NewGuid():N}";

        using var made = await host.PostAsync("/people", JsonSerializer.Serialize(new { name, function = "Auditoria", password = Initial }), cancellationToken);
        var answer = await made.Content.ReadAsStringAsync(cancellationToken);
        var entered = await host.EnterAsync(name, Initial, cancellationToken);

        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        Assert.DoesNotContain(Initial, answer, StringComparison.Ordinal);
        Assert.NotNull(entered);
        foreach (var table in new[] { "person", "person_change", "person_credential", "custody_event" })
        {
            Assert.Equal(0L, await host.NumberAsSuperuserAsync($"SELECT count(*) FROM public.{table} t WHERE t::text LIKE '%{Initial}%'", cancellationToken));
        }

        Assert.DoesNotContain(Initial, host.LogText, StringComparison.Ordinal);
    }
}
