namespace Core.Persistence.Migrations;

internal enum SchemaState
{
    UpToDate = 0,
    Behind = 1,
    Ahead = 2,
    Divergent = 3,
}
