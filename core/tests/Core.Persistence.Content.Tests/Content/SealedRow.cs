using Core.Persistence.Content;
using Core.Persistence.Content.Sealing;

namespace Core.Persistence.Tests.Content;

/// <summary>A sealed value with the wrapped key of the row it would be stored in.</summary>
internal sealed record SealedRow(SealedContent Content, ContentKey Key)
{
    public ReadOnlyMemory<byte> Envelope => Content.Envelope;
}
