namespace Zqx.Fixtures;

internal static class ThroughTheReader
{
    internal static async Task Read(dynamic context, dynamic sessions)
    {
        var form = await RequestReader.FormAsync(context);
        var account = RequestReader.SessionOf(context);
        var identifier = SessionCookie.Read(context.Request);
        var listed = sessions.ListOf(account, identifier);
    }
}
