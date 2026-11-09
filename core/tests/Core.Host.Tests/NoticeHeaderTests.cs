using Core.Host.Endpoints;
using Core.Host.Operators;
using Core.Host.Screens;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>
/// What the one who entered is told of the notices (issue 595, CA-2): the page they open carries how many are unread, so the header and the start say so, and the notices stay out of the
/// menu of work, which the start of whoever reaches nothing keeps empty (#594, CA-1).
/// </summary>
[Trait("Category", "Database")]
public sealed class NoticeHeaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task TheReachOfWhoEnteredCarriesHowManyNoticesAreUnread()
    {
        await using var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        var sessions = new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System);
        var staff = new OperatorAdministration(new PersonRegistry(source), new PersonCredentials(source, new PasswordDerivation(1)), sessions, new PersonNotices(source));
        var made = await staff.Registry.CreateAsync(new NewPerson($"zqx-pessoa-{Guid.NewGuid():N}", PersonFunctions.Accepted[0]), CustodyAuthor.System, Token);
        var person = made.PersonId!.Value;
        await staff.Notices.RecordPasswordResetAsync(person, 1, Token);
        await staff.Notices.RecordPasswordResetAsync(person, 2, Token);
        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(sessions).AddSingleton(staff).BuildServiceProvider() };
        context.Request.Headers.Cookie = $"__Host-sessao={sessions.Open(person)}";

        var reach = await ScreenGate.ReachOfAsync(context);

        Assert.Equal(2, reach.Unread);
    }

    [Fact]
    public async Task TheNoticesAreNotAnEntryOfTheMenuOfWork()
    {
        await using var app = HostApp();
        NoticeScreensEndpoint.Map(app);
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).ToArray();
        var services = new ServiceCollection().AddSingleton<EndpointDataSource>(new DefaultEndpointDataSource(endpoints)).BuildServiceProvider();

        var menu = ScreenMenu.Of(new DefaultHttpContext { RequestServices = services }, new ScreenReach(PersonFunctions.Administration, DomainGrants.None));

        Assert.Empty(menu);
    }

    private static WebApplication HostApp()
    {
        var builder = CompositionRoot.ClosedBuilder(HostProcess.FreePlainAddress().GetLeftPart(UriPartial.Authority));
        return builder.Build();
    }
}
