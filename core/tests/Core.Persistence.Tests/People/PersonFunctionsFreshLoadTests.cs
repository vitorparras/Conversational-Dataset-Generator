using System.Runtime.Loader;
using Core.Persistence.People;

namespace Core.Persistence.Tests.People;

/// <summary>
/// The list of functions is built once, when the type is first used, so a test that only reads it sees the list of the first use of the process. Here the assembly is loaded a second time,
/// in a context of its own, so that the list is built again and what it holds is what the code declares.
/// </summary>
public sealed class PersonFunctionsFreshLoadTests
{
    private static readonly string[] Declared = ["Anotação", "Arbitragem", "Curadoria do acervo", "Avaliação", "Auditoria", "Administração técnica"];

    [Fact]
    public void FunctionsOfAFreshLoadOfTheAssemblyAreTheSixOfThePolicy()
    {
        var context = new AssemblyLoadContext("person-functions-fresh", isCollectible: true);
        try
        {
            var functions = context.LoadFromAssemblyPath(typeof(PersonRegistry).Assembly.Location).GetType("Core.Persistence.People.PersonFunctions", throwOnError: true)!;

            var accepted = (IReadOnlyList<string>)functions.GetProperty("Accepted")!.GetValue(null)!;

            Assert.Equal<IEnumerable<string>>(Declared, accepted);
        }
        finally
        {
            context.Unload();
        }
    }
}
