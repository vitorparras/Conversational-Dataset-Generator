using Core.Persistence.People;

namespace Core.Persistence.Tests.People;

/// <summary>
/// The list of functions in the code is the table of the access policy, docs/privacidade/006-acesso.md, which binds the code (#109):
/// a function added or renamed in one place and not in the other fails here.
/// </summary>
public sealed class PersonFunctionsPolicyTests
{
    private const string PolicyFile = "docs/privacidade/006-acesso.md";
    private const string HeaderCell = "Função";

    [Fact]
    public void TheFunctionsOfTheCodeAreTheRowsOfThePolicyTable()
    {
        var policy = FunctionsOfThePolicy(File.ReadAllLines(PolicyPath()));

        Assert.Equal(policy.Order(StringComparer.Ordinal), PersonFunctions.Accepted.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TheCodeKeepsTheFunctionsInTheOrderOfThePolicyTable()
    {
        var policy = FunctionsOfThePolicy(File.ReadAllLines(PolicyPath()));

        Assert.Equal(policy, PersonFunctions.Accepted);
    }

    [Fact]
    public void TheReaderOfThePolicyTakesTheFirstCellOfEachRowBelowTheHeader()
    {
        string[] lines =
        [
            "Texto antes.",
            "| Função | O que alcança |",
            "|---|---|",
            "| Um | Alcance um |",
            "|  Dois  | Alcance dois |",
            "",
            "| Outra | Tabela |",
        ];

        Assert.Equal(["Um", "Dois"], FunctionsOfThePolicy(lines));
    }

    [Fact]
    public void ThePolicyTableHasRowsAtAll() => Assert.NotEmpty(FunctionsOfThePolicy(File.ReadAllLines(PolicyPath())));

    private static List<string> FunctionsOfThePolicy(string[] lines)
    {
        var rows = lines
            .SkipWhile(line => FirstCell(line) != HeaderCell)
            .Skip(1)
            .TakeWhile(line => line.StartsWith('|'))
            .Where(line => !FirstCell(line).StartsWith("---", StringComparison.Ordinal))
            .Select(FirstCell);
        return [.. rows];
    }

    private static string FirstCell(string line) =>
        line.StartsWith('|') ? line.Split('|')[1].Trim() : string.Empty;

    private static string PolicyPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docs", "privacidade", "006-acesso.md")))
        {
            directory = directory.Parent;
        }

        return Path.Combine((directory ?? throw new DirectoryNotFoundException($"{PolicyFile} was not found above the test binaries.")).FullName, "docs", "privacidade", "006-acesso.md");
    }
}
