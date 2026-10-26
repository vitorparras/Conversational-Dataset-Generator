using System.Security.Cryptography;
using System.Text;

namespace Core.Persistence.Migrations;

/// <summary>One numbered change to the schema: its text and the SHA-256 of the bytes that text came from.</summary>
public sealed class MigrationStep
{
    private MigrationStep(int version, string name, byte[] content)
    {
        Version = version;
        Name = name;
        Sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
        Sql = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(content);
    }

    /// <summary>The order of the step; steps run by ascending version.</summary>
    public int Version { get; }

    /// <summary>What the registry shows for the step, such as <c>0001-schema-version</c>.</summary>
    public string Name { get; }

    /// <summary>The lowercase hexadecimal digest of the step's bytes.</summary>
    public string Sha256 { get; }

    internal string Sql { get; }

    /// <summary>The step whose digest is taken over the bytes given, which are UTF-8 text.</summary>
    public static MigrationStep Of(int version, string name, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(content);
        return new MigrationStep(version, name, content);
    }
}
