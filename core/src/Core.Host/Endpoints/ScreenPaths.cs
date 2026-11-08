namespace Core.Host.Endpoints;

/// <summary>The routes of the pages a person walks through, written once so that the forms and the links of the screens and the routes that answer them cannot drift apart.</summary>
internal static class ScreenPaths
{
    internal const string SignIn = "/entrada";
    internal const string SignOut = "/saida";
    internal const string Home = "/";
    internal const string State = "/estado-do-sistema";
    internal const string Configuration = "/configuracao";
    internal const string Domains = "/dominios";
    internal const string Chain = "/custodia";
    internal const string ChainCheck = "/custodia/verificar";
}
