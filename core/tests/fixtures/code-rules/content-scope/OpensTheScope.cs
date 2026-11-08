namespace Fixture.ContentScope;

internal static class OpensTheScope
{
    public static Task Open(object source) => Transaction.BeginSystemAsync(source, 1, default);

    public const string Statement = Transaction.DeclareSystem;

    public static Task Snapshot(object source) => Transaction.BeginSnapshotAsync(source, 0, default);
}
