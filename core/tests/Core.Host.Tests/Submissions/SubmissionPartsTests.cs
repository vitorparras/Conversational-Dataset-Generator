using Core.Host.Submissions;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// The list of conditions the configuration declares reaches the opening of the batches (issue 85, CA-1; issue 75):
/// the first of the list is what a batch opened with none declared takes.
/// </summary>
public sealed class SubmissionPartsTests
{
    private const string Declared = "zqx_sigilosa, zqx_media,zqx_livre";

    private static readonly Dictionary<string, string> Values = new() { ["Ingestion:Conditions"] = Declared };

    [DatabaseFact]
    public async Task BatchOpenedByTheServiceTakesTheFirstConditionOfTheConfiguredList()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var batches = SubmissionParts.BatchesOf(world.DataSource, Values);

        var opened = await batches.OpenAsync(new BatchOpening(SubmissionWorld.Domain, SubmissionWorld.Person), TestContext.Current.CancellationToken);
        var read = await batches.ReadAsync(SubmissionWorld.Domain, opened.BatchId!.Value, TestContext.Current.CancellationToken);

        Assert.Equal("zqx_sigilosa", read!.Condition);
    }

    [DatabaseFact]
    public async Task ConditionOutsideTheConfiguredListIsRefusedWithTheValueFoundAndTheOnesAccepted()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var batches = SubmissionParts.BatchesOf(world.DataSource, Values);

        var refused = await batches.OpenAsync(new BatchOpening(SubmissionWorld.Domain, SubmissionWorld.Person, "zqx_livre_demais"), TestContext.Current.CancellationToken);

        Assert.Equal((BatchOutcome.ConditionUnknown, "zqx_livre_demais"), (refused.Outcome, refused.ConditionFound));
        Assert.Equal(["zqx_sigilosa", "zqx_media", "zqx_livre"], refused.ConditionsAccepted);
    }

    [DatabaseFact]
    public async Task TheContainerOfTheHostGivesTheBatchesAndTheirRegisterOverTheDatabaseOfTheStartup()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        await using var startup = new ServiceCollection().AddSingleton(world.DataSource).BuildServiceProvider();
        await using var host = Hosted(startup);

        var opened = await host.GetRequiredService<IngestionBatches>().OpenAsync(new BatchOpening(SubmissionWorld.Domain, SubmissionWorld.Person), TestContext.Current.CancellationToken);
        var entry = await host.GetRequiredService<BatchRegister>().ReadAsync([SubmissionWorld.Domain], opened.BatchId!.Value, TestContext.Current.CancellationToken);

        Assert.Equal((BatchState.Open, SubmissionWorld.Person), (entry!.State, entry.SubmittedBy));
    }

    [Fact]
    public void WithNoDatabaseDeclaredTheHostHasNoBatchesToOffer()
    {
        using var startup = new ServiceCollection().BuildServiceProvider();
        using var host = Hosted(startup);

        Assert.Null(host.GetService<IngestionBatches>());
        Assert.Null(host.GetService<BatchRegister>());
    }

    [Fact]
    public void ServiceNeedsEveryPieceItIsGivenAndSaysWhichIsMissing()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        var conditions = OriginConditions.Of(["a"]);
        var intake = SubmissionWorld.Intake(1, conditions);
        var batches = new IngestionBatches(dataSource, conditions);
        var register = new BatchRegister(dataSource);

        Assert.Equal(
            ["intake", "batches", "register", "dataSource"],
            [
                Assert.Throws<ArgumentNullException>(() => new SubmissionService(null!, batches, register, dataSource)).ParamName,
                Assert.Throws<ArgumentNullException>(() => new SubmissionService(intake, null!, register, dataSource)).ParamName,
                Assert.Throws<ArgumentNullException>(() => new SubmissionService(intake, batches, null!, dataSource)).ParamName,
                Assert.Throws<ArgumentNullException>(() => new SubmissionService(intake, batches, register, null!)).ParamName,
            ]);
    }

    [Fact]
    public async Task SubmissionWithNoRequestOrNoContentOrNoAuthorIsRefusedByName()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        var conditions = OriginConditions.Of(["a"]);
        var intake = SubmissionWorld.Intake(1, conditions);
        var service = new SubmissionService(intake, new IngestionBatches(dataSource, conditions), new BatchRegister(dataSource), dataSource);
        var request = SubmissionWorld.Request();

        Assert.Equal(
            ["request", "Content", "AuthorId"],
            [
                (await Assert.ThrowsAsync<ArgumentNullException>(() => service.SubmitAsync(null!, CancellationToken.None))).ParamName,
                (await Assert.ThrowsAsync<ArgumentNullException>(() => service.SubmitAsync(request with { Content = null! }, CancellationToken.None))).ParamName,
                (await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SubmitAsync(request with { AuthorId = 0 }, CancellationToken.None))).ParamName,
            ]);
    }

    private static ServiceProvider Hosted(ServiceProvider startup) =>
        new ServiceCollection()
            .AddSingleton(startup)
            .AddSingleton(provider => SubmissionParts.BatchesOf(provider, Values))
            .AddSingleton(SubmissionParts.RegisterOf)
            .BuildServiceProvider();
}
