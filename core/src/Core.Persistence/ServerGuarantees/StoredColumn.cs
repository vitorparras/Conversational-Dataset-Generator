namespace Core.Persistence.ServerGuarantees;

/// <summary>A column of the content schema as the catalog describes it: the table, the column, its type and the comment that marks it.</summary>
internal sealed record StoredColumn(string Table, string Name, string Type, string Comment)
{
    private const char Separator = '|';
    private const int TypeAt = 2;
    private const int CommentAt = 3;
    private const int Parts = 4;

    internal string Qualified => $"{Table}.{Name}";

    internal static StoredColumn Parse(string described)
    {
        var parts = described.Split(Separator, Parts);
        return new StoredColumn(parts[0], parts[1], parts[TypeAt], parts[CommentAt]);
    }
}
