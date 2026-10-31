using Core.Persistence.Content;

namespace Core.Persistence.ServerGuarantees;

/// <summary>What the catalog's columns of the content schema get wrong against the two lists of the code, in words that say what to change.</summary>
internal static class ProtectedColumnFindings
{
    private const string Marked = "protected:";

    /// <summary>The first column the lists do not account for, or null.</summary>
    internal static string? Stray(StoredColumn column) =>
        column.Comment.StartsWith(Marked, StringComparison.Ordinal) ? StrayProtected(column) : StrayClear(column);

    /// <summary>The first column of the lists that the schema lacks, or null.</summary>
    internal static string? Lacking(IEnumerable<StoredColumn> found)
    {
        var present = found.Select(column => column.Qualified).ToHashSet(StringComparer.Ordinal);
        var missing = ContentColumns.Protected.Select(column => $"{column.Table}.{column.Name}")
            .Concat(ContentColumns.Clear.Select(column => $"{column.Table}.{column.Name}"))
            .FirstOrDefault(listed => !present.Contains(listed));
        return missing is null ? null : $"a coluna {missing} das listas de conteúdo não existe no esquema content: aplique as migrações ou corrija ContentColumns";
    }

    private static string? StrayProtected(StoredColumn column)
    {
        var listed = ContentColumns.Protected.FirstOrDefault(candidate => candidate.Table == column.Table && candidate.Name == column.Name);
        return (listed, column.Type) switch
        {
            (null, _) => $"a coluna {column.Qualified} tem a marca de protegida e não consta em ContentColumns.Protected: inclua-a na lista, para o caminho de cifra conhecê-la",
            (_, not "bytea") => $"a coluna protegida {column.Qualified} é do tipo {column.Type} e não bytea: o cifrado se guarda como bytes",
            _ when listed.Mark != column.Comment => $"a coluna protegida {column.Qualified} tem a marca '{column.Comment}' e a lista diz '{listed.Mark}': a finalidade da chave tem de ser a mesma",
            _ => null,
        };
    }

    private static string? StrayClear(StoredColumn column)
    {
        var listed = ContentColumns.Clear.FirstOrDefault(candidate => candidate.Table == column.Table && candidate.Name == column.Name);
        return listed is null
            ? $"a coluna {column.Qualified} do esquema content não é protegida (sem a marca de finalidade) nem consta entre as colunas em claro: "
                + "se guarda texto de material, marque-a com COMMENT ON COLUMN e inclua-a em ContentColumns.Protected, como bytea; se não guarda, inclua-a em ContentColumns.Clear"
            : WrongType(column, listed);
    }

    private static string? WrongType(StoredColumn column, ClearColumn listed) =>
        column.Type == ClearTypes.Of(listed.Kind)
            ? null
            : $"a coluna {column.Qualified} está listada em claro como {listed.Kind}, do tipo {ClearTypes.Of(listed.Kind)}, e é do tipo {column.Type}";
}
