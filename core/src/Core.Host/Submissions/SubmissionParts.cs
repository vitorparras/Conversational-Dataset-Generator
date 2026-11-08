using Core.Persistence.Ingestion;
using Npgsql;

namespace Core.Host.Submissions;

/// <summary>Where the pieces of the submissions are taken from the configuration: the batches open under the list of conditions the installation declared, so that an omitted condition takes its first.</summary>
internal static class SubmissionParts
{
    /// <summary>The batches over the database, under the conditions the configuration gave, the most restrictive first.</summary>
    internal static IngestionBatches BatchesOf(NpgsqlDataSource source, IReadOnlyDictionary<string, string> values) =>
        new(source, AdmissionConfiguration.Conditions(values));

    /// <summary>The batches over the database the startup container holds; none when no database is declared.</summary>
    internal static IngestionBatches BatchesOf(IServiceProvider provider, IReadOnlyDictionary<string, string> values) =>
        provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source ? BatchesOf(source, values) : null!;

    /// <summary>The register of the batches over the database the startup container holds; none when no database is declared.</summary>
    internal static BatchRegister RegisterOf(IServiceProvider provider) =>
        provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source ? new BatchRegister(source) : null!;
}
