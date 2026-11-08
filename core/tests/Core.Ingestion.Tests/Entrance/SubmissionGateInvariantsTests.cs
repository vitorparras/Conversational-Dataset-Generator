using System.Reflection;
using System.Security.Cryptography;
using Core.Ingestion.Entrance;
using Core.Ingestion.Reading;
using Core.Tests.Support.Harness;
using CsCheck;

namespace Core.Ingestion.Tests.Entrance;

/// <summary>
/// Whatever the stream and the limit, the gate never reads past the limit and one byte, and the digest is the one of the bytes it read (issue 75, criterion 2);
/// and no type that leaves the gate has a place for the name the file came with (criterion 1 of the plan, the name that never crosses).
/// </summary>
public sealed class SubmissionGateInvariantsTests
{
    private static readonly string[] Names = ["Name", "File", "Path", "Extension"];

    private static readonly Gen<(byte[] Content, int Limit, int Piece)> Streams =
        Gen.Select(Gen.Byte.Array[0, 300], Gen.Int[1, 320], Gen.Int[1, 64], (content, limit, piece) => (content, limit, piece));

    [Fact]
    [Trait("Kind", "Property")]
    public void TheGateNeverReadsPastTheLimitAndOneByteAndTheDigestIsTheOneOfWhatItRead() =>
        PropertyRunner.FromProcess().Check(Streams, generated =>
        {
            var (content, limit, piece) = generated;
            var stream = new CountingStream(content, piece);
            var gate = new SubmissionGate(SourceReaders.Default(), limit, Guid.NewGuid);

            var result = gate.ReceiveAsync(new Submission(stream, "cdg-conversa/1", "n"), CancellationToken.None).GetAwaiter().GetResult();

            var expected = Math.Min(content.Length, limit + 1);
            Assert.Equal((expected, expected, true), ((int)result.BytesRead, (int)stream.Delivered, stream.LargestRequest <= limit + 1));
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content.AsSpan(0, expected))), result.Digest);
            Assert.Equal(content.Length > limit, result.Refusal == SubmissionRefusal.TooLarge);
        });

    [Fact]
    public void NoTypeThatLeavesTheGateHasAPlaceForTheNameTheFileCameWith()
    {
        var types = new[] { typeof(SubmissionResult), typeof(SubmissionId), typeof(SubmissionRefusal), typeof(ReadMaterial), typeof(ReadUnit), typeof(ReadOutcome) };

        var members = types.SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(member => member.MemberType is MemberTypes.Property or MemberTypes.Field)
            .Select(member => $"{type.Name}.{member.Name}")
            .Where(full => Array.Exists(Names, name => full.Contains(name, StringComparison.OrdinalIgnoreCase)))).ToList();

        Assert.Empty(members);
    }
}
