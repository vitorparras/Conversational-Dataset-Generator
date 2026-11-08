using Core.Persistence.Ingestion;
using Npgsql;

namespace Core.Host.Submissions;

/// <summary>Where the services of the ingestion are put together from the configuration: the batches open under the list of conditions the installation declared, so that an omitted condition takes its first.</summary>
internal static class IngestionServices
{
    /// <summary>Registers the batches and their register over the database the startup container holds; with no database declared the services are not there to be used.</summary>
    internal static IServiceCollection AddIngestion(IServiceCollection services, IReadOnlyDictionary<string, string> values) =>
        services
            .AddSingleton(provider => provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source ? BatchesOf(source, values) : null!)
            .AddSingleton(provider => provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source ? new BatchRegister(source) : null!);

    /// <summary>The batches over the database, under the conditions the configuration gave, the most restrictive first.</summary>
    internal static IngestionBatches BatchesOf(NpgsqlDataSource source, IReadOnlyDictionary<string, string> values) =>
        new(source, IngestionConfiguration.Conditions(values));
}
