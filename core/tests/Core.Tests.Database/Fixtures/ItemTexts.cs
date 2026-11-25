using System.Security.Cryptography;
using Core.Persistence.Content;
using Core.Persistence.Content.Writing;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>The texts of an item for the tests that are not about the occurrences: the decision is explicit, and it is that no occurrence was found.</summary>
public static class ItemTexts
{
    /// <summary>The steps of the sanitization the tests declare: one step, in its first version.</summary>
    public static readonly string Steps = "destinations@1";

    /// <summary>The version of <see cref="Steps"/>: the SHA-256 of the list, in lowercase hexadecimal.</summary>
    public static readonly string Version = "49ca4d5854062508ba965bbec6169a7898d926b3570c4deae45250f5a36d8700";

    /// <summary>The statement that records <see cref="Steps"/> as a version of the domain, for the tests that write an item with its sanitized text by hand.</summary>
    /// <param name="domain">The domain the version is recorded in.</param>
    public static string VersionRow(int domain) =>
        $"INSERT INTO content.sanitization_version (domain_id, version, steps) VALUES ({domain}, '{Version}', '{Steps}') ON CONFLICT DO NOTHING";

    /// <summary>The texts of an item that only has the raw text, with an empty list of occurrences as the decision.</summary>
    /// <param name="raw">The text as it arrived.</param>
    public static NewItemText Of(string raw) => new(raw, null, []);

    /// <summary>The texts of an item with the raw and the sanitized text, with an empty list of occurrences as the decision.</summary>
    /// <param name="raw">The text as it arrived.</param>
    /// <param name="sanitized">The sanitized text.</param>
    public static NewItemText Of(string raw, string sanitized) => new(raw, null, []) { Sanitization = Sanitization(sanitized) };

    /// <summary>
    /// The texts of an item with the raw, the sanitized and the pseudonymized text and no map, with an empty list of occurrences as the decision: the database refuses it at the commit
    /// for want of a map, so it serves the test of that refusal, or one that gives it a map before writing it. A test that wants the item written takes <see cref="PseudonymizedItem"/>.
    /// </summary>
    /// <param name="raw">The text as it arrived.</param>
    /// <param name="sanitized">The sanitized text.</param>
    /// <param name="pseudonymized">The pseudonymized text.</param>
    public static NewItemText Of(string raw, string sanitized, string pseudonymized) => new(raw, pseudonymized, []) { Sanitization = Sanitization(sanitized) };

    /// <summary>
    /// The texts of a pseudonymized item, with an empty list of occurrences as the decision and the map that reverses it: one entry under a marker drawn for the test, since a marker is
    /// held once in the installation and the tests share the database. Only the path that writes the item with its map takes it.
    /// </summary>
    /// <param name="raw">The text as it arrived.</param>
    /// <param name="sanitized">The sanitized text; null while it does not exist.</param>
    /// <param name="pseudonymized">The pseudonymized text.</param>
    public static NewItemText PseudonymizedItem(string raw, string? sanitized, string pseudonymized) =>
        new(raw, pseudonymized, [])
        {
            Sanitization = sanitized is null ? null : Sanitization(sanitized),
            Map = [new NewMapEntry(Marker(), "original sintetico zqx", false, OccurrenceDestination.Pseudonymize)],
        };

    /// <summary>A marker of a person drawn at random, so that tests sharing the database do not draw the same one.</summary>
    public static string Marker() => $"PESSOA_{RandomNumberGenerator.GetInt32(100_000_000):D8}";

    /// <summary>What a sanitization of one step that made no edit leaves with an item, with the sanitized text given.</summary>
    /// <param name="sanitized">The sanitized text.</param>
    public static ItemSanitization Sanitization(string sanitized)
    {
        ArgumentNullException.ThrowIfNull(sanitized);
        return new() { Text = sanitized, Version = Version, Steps = Steps, Trail = $$"""{"raw":{{sanitized.Length}},"steps":[[]]}""", Counts = [0], Untreated = [0] };
    }
}
