namespace Core.Persistence.Migrations;

internal sealed record AppliedStep(int Version, string Name, string Sha256);
