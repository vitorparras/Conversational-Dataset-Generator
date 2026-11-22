using System.Diagnostics.CodeAnalysis;
using Core.Host.Endpoints;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The case of the screens of the rerun of the sanitization (issue 97) with the real routes and the desk the root registers, over a database with one domain, <see cref="Domain"/>, granted to the
/// person of the curation, and <see cref="Items"/> items of an accepted batch sanitized by an older version of the steps. No worker runs: the queue moves when a test moves it.
/// </summary>
internal sealed class RerunCase : IAsyncDisposable
{
    /// <summary>The domain the curation has granted, which is the active one.</summary>
    public const string Domain = "zqx-dominio-a";

    /// <summary>How many items the accepted batch holds.</summary>
    public const int Items = 3;

    private RerunCase(ScreenHost host)
    {
        Host = host;
    }

    public ScreenHost Host { get; }

    [SuppressMessage("Reliability", "CA2000", Justification = "The host goes to the case that is returned, which disposes it.")]
    public static async Task<RerunCase> StartAsync(CancellationToken cancellationToken)
    {
        var host = await ScreenHost.StartAsync(
            new ScreenParts
            {
                Services = (services, setup) =>
                    _ = services.AddSingleton<DomainGrants>(provider => new GrantedToTheCurator(provider.GetRequiredService<NpgsqlDataSource>(), setup.People.Curator))
                        .AddSingleton(provider => new ServiceCollection().AddSingleton(provider.GetRequiredService<NpgsqlDataSource>()).BuildServiceProvider()),
                Screens = BatchRoutes.Map,
                Prepare = SeedAsync,
            },
            cancellationToken);
        return new RerunCase(host);
    }

    /// <summary>Asks the rerun as the person of the curation, or finds the one open, and refuses each of its items at the last try, as a failing worker would; the request is then done.</summary>
    public async Task<long> RefusedEveryItemAsync()
    {
        var asked = await Host.Services.GetRequiredService<RerunDesk>().RequestAsync(1, Host.CuratorId, CancellationToken.None);
        await using var source = NpgsqlDataSource.Create(Host.Database.AppConnectionString);
        var queue = new SanitizationReruns(source, TimeProvider.System);
        while (await queue.TakeAsync(1, CompositionRoot.StepsOf().Version, CancellationToken.None) is { } hold)
        {
            _ = await queue.FailAsync(hold, CancellationToken.None);
        }

        return asked.RerunId ?? throw new InvalidOperationException($"The request ended in {asked.Outcome}.");
    }

    public ValueTask DisposeAsync() => Host.DisposeAsync();

    private static Task SeedAsync(ScratchDatabase database) =>
        database.RunAsOwnerAsync(
            [
                $"INSERT INTO public.domain (name) VALUES ('{Domain}')",
                .. SeededBatches.Statements(1),
                "SELECT set_config('app.domain', '1', false)",
                ItemTexts.VersionRow(1),
                "INSERT INTO content.item_text (domain_id, batch_id, raw_text, sanitized_text, pseudonymized_text, dek_epoch, dek_wrapped, sanitization_version, sanitization_trail, "
                + $"sanitization_counts) SELECT 1, 1, '\x01', '\x02', 'zqx', 1, '\x03', '{ItemTexts.Version}', '\x04', '{{0}}' FROM generate_series(1, {Items})",
                "SELECT set_config('app.domain', '', false)",
            ]);

    /// <summary>The source of grants of the test: the domain to the person of the curation and none to anyone else.</summary>
    private sealed class GrantedToTheCurator(NpgsqlDataSource source, string curator) : DomainGrants
    {
        internal override async Task<IEnumerable<string>> GrantedToAsync(long entered, CancellationToken cancellationToken)
        {
            await using var command = source.CreateCommand("SELECT name FROM public.person WHERE id = @id");
            _ = command.Parameters.AddWithValue("id", entered);
            return string.Equals((string?)await command.ExecuteScalarAsync(cancellationToken), curator, StringComparison.Ordinal) ? Enumerable.Repeat(Domain, 1) : None;
        }
    }
}
