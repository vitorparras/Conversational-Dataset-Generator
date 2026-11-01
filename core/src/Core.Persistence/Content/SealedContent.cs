namespace Core.Persistence.Content;

/// <summary>
/// The envelope of a protected column's value, ready to be stored. Only <see cref="ProtectedContent"/> makes one, so a statement that
/// writes a protected column has nothing but sealed content to write.
/// </summary>
public sealed record SealedContent
{
    private readonly byte[] envelope;

    internal SealedContent(ProtectedColumn column, int domainId, byte[] envelope)
    {
        Column = column;
        DomainId = domainId;
        this.envelope = envelope;
    }

    /// <summary>The column the content was sealed for, the only one it may be stored in.</summary>
    public ProtectedColumn Column { get; }

    /// <summary>The domain whose key sealed it, the only one whose row may hold it.</summary>
    public int DomainId { get; }

    /// <summary>The bytes to store.</summary>
    public ReadOnlyMemory<byte> Envelope => envelope;
}
