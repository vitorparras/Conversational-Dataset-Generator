using System.Reflection;
using System.Text.Json;
using Core.Boundary.Contracts;

namespace Core.Boundary.Tests;

/// <summary>
/// The options of the contract built again, at the moment of the call. <see cref="ContractJson.Options"/> is built once, when the
/// process starts and before any test runs, so a change to the building is only seen by a build made during the test.
/// </summary>
internal static class RebuiltContractJson
{
    public static JsonSerializerOptions Options() =>
        (JsonSerializerOptions)(typeof(ContractJson).GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ContractJson.Create was not found; the options test must follow it."))
            .Invoke(null, null)!;
}
