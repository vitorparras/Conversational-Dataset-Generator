namespace Zqx.Fixtures;

internal static class ReadsByHand
{
    internal static async Task Read(dynamic context, dynamic sessions)
    {
        var carries = context.Request.HasFormContentType;
        var form = await context.Request.ReadFormAsync();
        var typed = context.Request.Form["name"];
        var account = sessions.Resolve(SessionCookie.Read(context.Request));
    }
}
