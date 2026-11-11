using System.Globalization;
using Core.Host.Screens;
using Core.Persistence.Content.Emptying;
using Core.Persistence.People;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The screens of the emptying of an item (#617, decision 0051), reached only by the curation of the collection with a domain granted: the entry by the identifier,
/// the confirmation on a page of its own and the act, which is a POST that ends in a 303 and never in a dialog of the browser (<see cref="EmptyingActEndpoint"/>).
/// The emptying is the one of the desk (<see cref="ItemEmptyingDesk"/>); nothing here decides who may empty or which item is found.
/// The screen says only what is known without reading a text: an item that was emptied is shown with its record, and one that does not
/// exist is found out by the act, which answers on the form itself and never by an address. The domain is the active one, which until #112 is the first granted.
/// </summary>
internal static class EmptyingScreensEndpoint
{
    private const int Place = 93;
    private const string Absent = "Nenhum item do domínio tem esse identificador.";
    private const string NotANumber = "O identificador é um número inteiro maior que zero.";

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(ScreenPaths.Emptying, ShowAsync).WithMetadata(ScreenAccess.Of(EmptyingScreen.Title, Place, PersonFunctions.Curation));
        _ = endpoints.MapGet(ScreenPaths.EmptyingConfirm, EmptyingActEndpoint.ConfirmAsync).WithMetadata(ScreenAccess.WithoutEntry(PersonFunctions.Curation));
        _ = endpoints.MapPost(ScreenPaths.EmptyingConfirm, EmptyingActEndpoint.EmptyAsync).WithMetadata(ScreenAccess.WithoutEntry(PersonFunctions.Curation));
    }

    /// <summary>The active domain of whoever entered, the first granted until #112, with its identifier; none when no domain is granted or the name is of no domain.</summary>
    internal static async Task<ActiveDomain?> ActiveAsync(HttpContext context, ItemEmptyingDesk desk)
    {
        var granted = context.Features.Get<ScreenReach>()?.Granted.FirstOrDefault();
        return granted is not null && await desk.DomainIdOfAsync(granted, context.RequestAborted) is { } id ? new ActiveDomain(granted, id) : null;
    }

    internal static bool TryItem(string typed, out long item) =>
        long.TryParse(typed, NumberStyles.None, CultureInfo.InvariantCulture, out item) && item > 0;

    /// <summary>The entry of the screen with the identifier given, as typed or as it is.</summary>
    internal static string Back(string typed) => $"{ScreenPaths.Emptying}?{EmptyingScreen.ItemField}={Uri.EscapeDataString(typed)}";

    internal static string Back(long item) => Back(item.ToString(CultureInfo.InvariantCulture));

    private static async Task<IResult> ShowAsync(HttpContext context, [FromServices] ItemEmptyingDesk? desk)
    {
        if (desk is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var domain = await ActiveAsync(context, desk);
        return domain is null ? ScreenResult.NotFound(context) : await LookedUpAsync(context, desk, domain);
    }

    private static async Task<IResult> LookedUpAsync(HttpContext context, ItemEmptyingDesk desk, ActiveDomain domain)
    {
        var typed = context.Request.Query[EmptyingScreen.ItemField].ToString();
        IDictionary<string, object?> screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(EmptyingScreen.Domain)] = domain.Name,
            [nameof(EmptyingScreen.Typed)] = typed,
        };
        if (typed.Length > 0)
        {
            await AddItemAsync(context, (desk, domain), screen);
        }

        return ScreenResult.Of(context, typeof(EmptyingScreen), EmptyingScreen.Title, screen);
    }

    /// <summary>What the item typed shows: the refusal of a number that is not one, or the item with the record of its emptying when it has one.</summary>
    private static async Task AddItemAsync(HttpContext context, (ItemEmptyingDesk Desk, ActiveDomain Domain) at, IDictionary<string, object?> screen)
    {
        if (!TryItem(context.Request.Query[EmptyingScreen.ItemField].ToString(), out var item))
        {
            screen[nameof(EmptyingScreen.LookupError)] = NotANumber;
            return;
        }

        var record = await at.Desk.RecordOfAsync(at.Domain.Id, item, context.RequestAborted);

        screen[nameof(EmptyingScreen.Shown)] = new EmptyingScreen.Item(item, record is null ? null : Shown(record));
        screen[nameof(EmptyingScreen.JustEmptied)] = record is not null && context.Request.Query.ContainsKey(EmptyingScreen.DoneField);
    }

    /// <summary>
    /// The entry form answered to the act that found no item of the domain, with the refusal on the field. It is the answer of the POST itself and never an address,
    /// so no link can make the screen say that an item that exists does not.
    /// </summary>
    internal static RazorComponentResult<ScreenFrame> Missing(HttpContext context, ActiveDomain domain, long item) =>
        ScreenResult.Of(context, typeof(EmptyingScreen), EmptyingScreen.Title, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(EmptyingScreen.Domain)] = domain.Name,
            [nameof(EmptyingScreen.Typed)] = item.ToString(CultureInfo.InvariantCulture),
            [nameof(EmptyingScreen.LookupError)] = Absent,
        });

    private static EmptyingScreen.Record Shown(EmptiedItem record) => new(record.At, record.Executor, record.Motive, record.Reference);

    /// <summary>The domain the screens work in: its name, which the pages say, and its identifier, which the server asks by.</summary>
    internal sealed record ActiveDomain(string Name, int Id);
}
