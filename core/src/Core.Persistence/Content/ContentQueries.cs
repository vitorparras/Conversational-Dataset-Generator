using System.Collections.Frozen;
using System.Reflection;

namespace Core.Persistence.Content;

/// <summary>
/// Every query the system exposes over content, by name, so that a test can run each one with no domain declared and demand that it returns
/// nothing. A query is a <c>public const string</c> field of this class, and the list is read from the fields, so adding one is all it takes
/// to be tested; the architecture rule keeps every statement over content in such a constant of this file. It starts empty because no content
/// table exists yet.
/// </summary>
public static class ContentQueries
{
    /// <summary>The queries over content, as the statement each one runs, by the name of its constant.</summary>
    public static IReadOnlyDictionary<string, string> All { get; } = Declared(typeof(ContentQueries));

    /// <summary>The public string constants the type declares, by name.</summary>
    internal static FrozenDictionary<string, string> Declared(IReflect type) =>
        type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .ToFrozenDictionary(field => field.Name, field => (string)field.GetRawConstantValue()!, StringComparer.Ordinal);
}
