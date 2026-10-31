namespace Fixture.ProtectedColumns;

internal static class NeverFiltersOrOrders
{
    public const string Insert = "INSERT INTO t (id, raw_text, sanitized_text) VALUES (@id, @raw, @sanitized)";

    public const string Select = "SELECT id, raw_text, entries FROM t WHERE id = @id ORDER BY id";

    public const string Update = "UPDATE t SET raw_text = @raw WHERE id = @id RETURNING sanitized_text";

    public const string Upsert = "INSERT INTO t (id, raw_text) VALUES (@id, @raw) ON CONFLICT (id) DO UPDATE SET raw_text = EXCLUDED.raw_text";

    public const string Parameter = "SELECT id FROM t WHERE id = @raw_text";

    public const string Sentence = "the raw_text is where the entries go, ordered by nothing";

    public const string Subselect = "SELECT id FROM t WHERE id IN (SELECT item_id FROM u ORDER BY id) ORDER BY id";

    public const string Limited = "SELECT raw_text FROM t WHERE id = @id LIMIT 1";

    public const string OtherColumns = "SELECT id FROM t WHERE pseudonymized_text = @text ORDER BY recorded_at";

    public static string Interpolated(int id) => $"SELECT raw_text FROM t WHERE id = {id}";
}
