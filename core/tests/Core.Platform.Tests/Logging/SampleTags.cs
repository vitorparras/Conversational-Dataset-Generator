using System.Collections;

namespace Core.Platform.Tests.Logging;

/// <summary>The structured state of a log event: the key and value pairs a message was written with.</summary>
internal sealed class SampleTags(KeyValuePair<string, object?>[] pairs) : IReadOnlyList<KeyValuePair<string, object?>>
{
    public int Count => pairs.Length;

    public KeyValuePair<string, object?> this[int index] => pairs[index];

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, object?>>)pairs).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
