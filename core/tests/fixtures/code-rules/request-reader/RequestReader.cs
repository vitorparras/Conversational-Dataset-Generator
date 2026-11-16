namespace Zqx.Fixtures;

internal static class RequestReader
{
    internal static async Task Read(dynamic context, dynamic sessions)
    {
        var carries = context.Request.HasFormContentType;
        var form = await context.Request.ReadFormAsync();
        var account = sessions.Resolve(SessionCookie.Read(context.Request));
    }
}
