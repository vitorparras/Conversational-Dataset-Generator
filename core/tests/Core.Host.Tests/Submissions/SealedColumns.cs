using System.Globalization;
using Core.Persistence.Content.Sealing;

namespace Core.Host.Tests.Submissions;

/// <summary>The sealed columns of the world read as the owner and opened with the key of each row, for the tests to see what no reading of production shows together.</summary>
internal static class SealedColumns
{
    /// <summary>The sealed column given of every row of the table that the condition keeps, opened with the key of its row, in the order of the items.</summary>
    /// <param name="world">The world whose database and content are read.</param>
    /// <param name="column">The sealed column.</param>
    /// <param name="table">The table of the column, in the content schema.</param>
    /// <param name="where">The condition of the rows, empty for every row.</param>
    internal static async Task<string[]> OpenedAsync(SubmissionWorld world, ProtectedColumn column, string table, string where = "")
    {
        var order = table == "item_text" ? "id" : "item_id";
        var rows = await world.StoredAsync(
            $"SELECT encode({column.Name}, 'base64') || ',' || dek_epoch || ',' || encode(dek_wrapped, 'base64') FROM content.{table} {where} ORDER BY {order}");
        return [.. rows.Select(row => row.Split(',')).Select(parts => world.Content.Open(
            column, SubmissionWorld.Domain, Convert.FromBase64String(parts[0]), new ContentKey(int.Parse(parts[1], CultureInfo.InvariantCulture), Convert.FromBase64String(parts[2]))))];
    }
}
