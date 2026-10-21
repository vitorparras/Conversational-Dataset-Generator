using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Core.Boundary.Contracts;

namespace Core.Boundary.Http;

/// <summary>
/// Classifies what puts an answer outside the contract, reading the shape from the contract's own JSON metadata so
/// that no second copy of it exists; the deserializer stays the judge of every value.
/// </summary>
internal static class ContractShape
{
    /// <summary>The first unknown or missing field, or a null or non-object where the contract needs an object; null when the shape fits.</summary>
    internal static ValidationProblem? FirstProblem(JsonElement answer) => Check(answer, typeof(AnalysisResponse));

    private static ValidationProblem? Check(JsonElement element, Type type)
    {
        var info = ContractJson.Options.GetTypeInfo(type);
        return (info.Kind, element.ValueKind) switch
        {
            (JsonTypeInfoKind.Object, JsonValueKind.Object) => CheckObject(element, info),
            (JsonTypeInfoKind.Object, _) => ValidationProblem.WrongValue,
            (JsonTypeInfoKind.Enumerable, JsonValueKind.Array) => FirstOf(element.EnumerateArray().Select(item => Check(item, info.ElementType!))),
            _ => null,
        };
    }

    private static ValidationProblem? CheckObject(JsonElement element, JsonTypeInfo info)
    {
        if (element.EnumerateObject().Any(field => !info.Properties.Any(declared => field.NameEquals(declared.Name))))
        {
            return ValidationProblem.UnknownField;
        }

        if (info.Properties.Any(declared => declared.IsRequired && !element.TryGetProperty(declared.Name, out _)))
        {
            return ValidationProblem.MissingField;
        }

        return FirstOf(info.Properties.Select(declared =>
            element.TryGetProperty(declared.Name, out var value) ? Check(value, declared.PropertyType) : null));
    }

    private static ValidationProblem? FirstOf(IEnumerable<ValidationProblem?> problems) =>
        problems.FirstOrDefault(problem => problem is not null);
}
