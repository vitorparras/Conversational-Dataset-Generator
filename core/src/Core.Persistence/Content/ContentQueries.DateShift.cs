namespace Core.Persistence.Content;

/// <summary>The statements of the shift of the dates (decision 0020): the one row of the domain declared, sealed, and the key that wraps it.</summary>
public static partial class ContentQueries
{
    /// <summary>The sealed shift of the domain declared and the key of its row wrapped with its epoch; the domain holds one row at most.</summary>
    internal const string ReadDateShift = "SELECT shift, dek_epoch, dek_wrapped FROM content.date_shift";

    /// <summary>Keeps the shift drawn for the domain declared unless the domain already holds one, which a second draw loses to.</summary>
    internal const string InsertDateShift =
        "INSERT INTO content.date_shift (domain_id, shift, dek_epoch, dek_wrapped) VALUES (@domain, @shift, @epoch, @wrapped) ON CONFLICT (domain_id) DO NOTHING";

    /// <summary>The keys, wrapped, that an epoch older than the newest wraps, among the shifts; a page of them.</summary>
    internal const string ReadOlderShiftKeys = "SELECT id, dek_epoch, dek_wrapped FROM content.date_shift WHERE dek_epoch < @newest ORDER BY id LIMIT 500";

    internal const string ReadShiftKeysAfter = "SELECT id, dek_epoch, dek_wrapped FROM content.date_shift WHERE id > @after ORDER BY id LIMIT @batch";

    /// <summary>Wraps again the key of a shift; only the wrapped key and its epoch change.</summary>
    internal const string RewrapShiftKey = "UPDATE content.date_shift SET dek_epoch = @epoch, dek_wrapped = @wrapped WHERE id = @id";
}
