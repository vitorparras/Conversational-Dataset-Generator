using Core.Host.Submissions;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
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

    private static readonly Dictionary<string, string> Values = new() { ["Ingestion:Conditions"] = Declared, [AdmissionConfiguration.MaxBytesPath] = "10485760" };

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
        var intake = SubmissionWorld.Intake(1, conditions, SubmissionWorld.SealingOf(["texto-bruto/1/v1"]));
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
        var intake = SubmissionWorld.Intake(1, conditions, SubmissionWorld.SealingOf(["texto-bruto/1/v1"]));
        var service = new SubmissionService(intake, new IngestionBatches(dataSource, conditions), new BatchRegister(dataSource), dataSource);
        var request = SubmissionWorld.Request();

        Assert.Equal(
            ["request", "request.Content", "request.AuthorId"],
            [
                (await Assert.ThrowsAsync<ArgumentNullException>(() => service.SubmitAsync(null!, CancellationToken.None))).ParamName,
                (await Assert.ThrowsAsync<ArgumentNullException>(() => service.SubmitAsync(request with { Content = null! }, CancellationToken.None))).ParamName,
                (await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SubmitAsync(request with { AuthorId = 0 }, CancellationToken.None))).ParamName,
            ]);
    }

    [DatabaseFact]
    public async Task EachUseReadsTheKeyAndTheLabelsAgainSoADomainCreatedAfterTheFirstSubmissionIsKnownToTheNext()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        await using var startup = new ServiceCollection().AddSingleton(world.DataSource).AddSingleton<IMasterKeySource>(new FixedKeySource(present: true)).BuildServiceProvider();
        await world.Database.RunAsOwnerAsync("INSERT INTO public.derivation_label (label) VALUES ('texto-bruto/1/v1'), ('texto-bruto/2/v1')");
        await using var host = Hosted(startup);
        var holder = host.GetRequiredService<SubmissionServiceHolder>();

        using var first = await holder.GetAsync(TestContext.Current.CancellationToken);
        var before = await first!.Service.SubmitAsync(SubmissionWorld.Request(), TestContext.Current.CancellationToken);
        await world.Database.RunAsOwnerAsync("INSERT INTO public.domain (name) VALUES ('zqx-domain-c')", "INSERT INTO public.derivation_label (label) VALUES ('texto-bruto/3/v1')");
        using var next = await holder.GetAsync(TestContext.Current.CancellationToken);
        var after = await next!.Service.SubmitAsync(SubmissionWorld.Request() with { DomainId = 3 }, TestContext.Current.CancellationToken);

        Assert.Equal((null, SubmissionKind.Done, SubmissionKind.Done), (before.Failure, before.Kind, after.Kind));
        Assert.NotSame(first.Service, next.Service);
    }

    [DatabaseFact]
    public async Task KeyThatCannotBeReadGivesNoServiceAndTheNextUseTriesAgain()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var source = new FixedKeySource(present: false);
        await using var startup = new ServiceCollection().AddSingleton(world.DataSource).AddSingleton<IMasterKeySource>(source).BuildServiceProvider();
        await using var host = Hosted(startup);
        var holder = host.GetRequiredService<SubmissionServiceHolder>();

        var absent = await holder.GetAsync(TestContext.Current.CancellationToken);
        source.Present = true;
        using var present = await holder.GetAsync(TestContext.Current.CancellationToken);

        Assert.Null(absent);
        Assert.NotNull(present);
        Assert.Equal(2, source.Reads);
    }

    [DatabaseFact]
    public async Task TheDoorAnswersFromTheDatabaseAloneWhateverTheKey()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        await using var startup = new ServiceCollection().AddSingleton(world.DataSource).AddSingleton<IMasterKeySource>(new FixedKeySource(present: false)).BuildServiceProvider();
        await using var host = Hosted(startup);

        var status = await host.GetRequiredService<IntakeDoor>().StatusAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new SubmissionGateStatus(true, null), status);
    }

    private static ServiceProvider Hosted(ServiceProvider startup) =>
        new ServiceCollection()
            .AddSingleton(startup)
            .AddSingleton(provider => SubmissionParts.BatchesOf(provider, Values))
            .AddSingleton(SubmissionParts.RegisterOf)
            .AddSingleton(SubmissionParts.DoorOf)
            .AddSingleton(provider => SubmissionParts.ServicesOf(provider, Values))
            .BuildServiceProvider();

    /// <summary>A source of the master key that gives it, with the labels of three domains, or does not, as the test asks; each reading is a new one, as the file is read again.</summary>
    private sealed class FixedKeySource : IMasterKeySource
    {
        private static readonly string[] Labels = ["texto-bruto/1/v1", "texto-bruto/2/v1", "texto-bruto/3/v1"];

        internal FixedKeySource(bool present)
        {
            Present = present;
        }

        public string Origin => "zqx-test";

        internal bool Present { get; set; }

        internal int Reads { get; private set; }

        public Task<MasterKeyReading> ReadAsync(CancellationToken cancellationToken)
        {
            Reads++;
            var key = Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0x5A ^ ((index * 29) + 3))).ToArray();
            var secret = Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0xA7 ^ ((index * 31) + 11))).ToArray();
            var epochs = CustodyEpochs.Of(new Dictionary<int, byte[]> { [1] = secret });
            return Task.FromResult(Present ? MasterKeyReading.FromBytes(key, DerivationLabels.Of(Labels), epochs, "zqx-test") : MasterKeyReading.Absent("zqx-test"));
        }
    }
}
