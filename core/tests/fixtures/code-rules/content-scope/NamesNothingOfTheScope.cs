namespace Fixture.ContentScope;

internal static class NamesNothingOfTheScope
{
    public const string Domain = "SELECT set_config('app.domain', @domain, true)";

    public static Task Open(object source) => Transaction.BeginAsync(source, 1, default);
}
