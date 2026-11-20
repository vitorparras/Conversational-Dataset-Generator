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
    public static NewItemText Of(string raw, string sanitized) => Of(raw, sanitized, null);

    /// <summary>The texts of an item with the three texts, with an empty list of occurrences as the decision.</summary>
    /// <param name="raw">The text as it arrived.</param>
    /// <param name="sanitized">The sanitized text; null while it does not exist.</param>
    /// <param name="pseudonymized">The pseudonymized text; null while it does not exist.</param>
    public static NewItemText Of(string raw, string? sanitized, string? pseudonymized) =>
        new(raw, pseudonymized, []) { Sanitization = sanitized is null ? null : Sanitization(sanitized) };

    /// <summary>What a sanitization of one step that made no edit leaves with an item, with the sanitized text given.</summary>
    /// <param name="sanitized">The sanitized text.</param>
    public static ItemSanitization Sanitization(string sanitized)
    {
        ArgumentNullException.ThrowIfNull(sanitized);
        return new() { Text = sanitized, Version = Version, Steps = Steps, Trail = $$"""{"raw":{{sanitized.Length}},"steps":[[]]}""", Counts = [0], Untreated = [0] };
    }
}
