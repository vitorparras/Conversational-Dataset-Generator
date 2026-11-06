namespace Core.Ingestion.Tests;

/// <summary>Units and materials built for the tests, with the texts marked so that a text found in a refusal is a text that leaked.</summary>
internal static class Units
{
    public static readonly DateTimeOffset First = new(2031, 3, 4, 8, 0, 0, TimeSpan.Zero);

    public static ReadUnit Plain(string text) => new(text, null, null, null, []);

    public static ReadUnit Message(string text, string? author, int? second) =>
        new(text, author, second is { } at ? First.AddSeconds(at) : null, null, []);

    public static ReadUnit Labeled(string text, params Passage[] passages) => new(text, null, null, null, passages);

    public static ReadOutcome Read(params ReadUnit[] units) => ReadOutcome.Read(new ReadMaterial(units));
}
