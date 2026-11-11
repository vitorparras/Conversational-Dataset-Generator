using System.Globalization;
using Core.Host.Screens;
using Core.Host.Settings;
using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Platform.Configuration;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The screen of the changes of configuration (A9, #634; decision 0053), for the technical administration alone: a actor proposes a key of the list, a value and a motive, which the rules
/// of the start judge before anything is written, and another actor approves or refuses what waits. The actor is the one the session names, never one the form says. An act done takes
/// the browser back to the screen with 303, which says what was done; a refusal answers the page again with the reason on the field it is about. The file is never written here.
/// </summary>
internal static class ConfigurationChangeScreensEndpoint
{
    private const int ChangesPlace = 92;
    private const int ShownNotApplied = 20;

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(ScreenPaths.ConfigurationChanges, ShowAsync)
            .WithMetadata(ScreenAccess.Of(ConfigurationChangeScreen.Title, ChangesPlace, PersonFunctions.Administration));
        _ = endpoints.MapPost(ScreenPaths.ConfigurationChanges, SubmitAsync).WithMetadata(ScreenAccess.Unlisted(PersonFunctions.Administration));
        _ = endpoints.MapPost(ScreenPaths.ConfigurationDecision, DecideAsync).WithMetadata(ScreenAccess.Unlisted(PersonFunctions.Administration));
    }

    private static async Task<IResult> ShowAsync(HttpContext context, [FromServices] ChangeDecisionDesk? decisions, [FromServices] EffectiveConfiguration configuration) =>
        decisions is null || ActorOf(context) is not { } actor
            ? ScreenResult.Unavailable(context)
            : await PageAsync(context, decisions, configuration, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [nameof(ConfigurationChangeScreen.Actor)] = actor,
                [nameof(ConfigurationChangeScreen.Done)] = ChangeTexts.Done(AccountNotice.Take(context)?.Done),
            });

    private static async Task<IResult> SubmitAsync(
        HttpContext context, [FromServices] ConfigurationChangeDesk? desk, [FromServices] ChangeDecisionDesk? decisions, [FromServices] EffectiveConfiguration configuration)
    {
        if (desk is null || decisions is null || ActorOf(context) is not { } actor)
        {
            return ScreenResult.Unavailable(context);
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var request = new ChangeRequest(
            form[ConfigurationChangeScreen.KeyField].ToString(), form[ConfigurationChangeScreen.ValueField].ToString(), form[ConfigurationChangeScreen.MotiveField].ToString(), actor);
        var answer = await desk.SubmitAsync(request, context.RequestAborted);
        if (answer.Outcome == ChangeOutcome.Recorded)
        {
            return Back(context, ChangeTexts.SubmittedDone);
        }

        var refusedKey = answer.Outcome is ChangeOutcome.OnlyOnServer or ChangeOutcome.OverriddenOnServer;
        var refusedMotive = answer.Outcome == ChangeOutcome.MotiveUnknown;
        var refusedForm = answer.Outcome is ChangeOutcome.WithoutFunction or ChangeOutcome.NotRecorded;
        IDictionary<string, object?> screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(ConfigurationChangeScreen.Actor)] = actor,
            [nameof(ConfigurationChangeScreen.Key)] = request.Key,
            [nameof(ConfigurationChangeScreen.Value)] = request.Value,
            [nameof(ConfigurationChangeScreen.Motive)] = request.Motive,
            [nameof(ConfigurationChangeScreen.KeyError)] = refusedKey ? ChangeTexts.Refused(answer) : null,
            [nameof(ConfigurationChangeScreen.ValueError)] = ChangeTexts.Value(answer),
            [nameof(ConfigurationChangeScreen.MotiveError)] = refusedMotive ? ChangeTexts.Refused(answer) : null,
            [nameof(ConfigurationChangeScreen.FormError)] = refusedForm ? ChangeTexts.Refused(answer) : null,
        };
        return await PageAsync(context, decisions, configuration, screen);
    }

    /// <summary>Approves or refuses the submission the form names; an identifier that is not a number, or a decision that is neither, leads nowhere and decides nothing.</summary>
    private static async Task<IResult> DecideAsync(HttpContext context, [FromServices] ChangeDecisionDesk? decisions, [FromServices] EffectiveConfiguration configuration)
    {
        if (decisions is null || ActorOf(context) is not { } actor)
        {
            return ScreenResult.Unavailable(context);
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var decision = form[ConfigurationChangeScreen.DecisionField].ToString();
        if (!long.TryParse(form[ConfigurationChangeScreen.ChangeField], NumberStyles.None, CultureInfo.InvariantCulture, out var changeId)
            || decision is not (ConfigurationChangeScreen.Approve or ConfigurationChangeScreen.Refuse))
        {
            return ScreenResult.NotFound(context);
        }

        var approved = decision == ConfigurationChangeScreen.Approve;
        var outcome = await decisions.DecideAsync(new DecisionRequest(changeId, approved, actor), context.RequestAborted);
        if (outcome == DecisionOutcome.Decided)
        {
            return Back(context, approved ? ChangeTexts.ApprovedDone : ChangeTexts.RefusedDone);
        }

        IDictionary<string, object?> screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(ConfigurationChangeScreen.Actor)] = actor,
            [nameof(ConfigurationChangeScreen.DecisionError)] = ChangeTexts.Decision(outcome),
        };
        return await PageAsync(context, decisions, configuration, screen);
    }

    /// <summary>The page with what waits for a decision and the keys the screen changes, each with its value in force, besides what the act being answered gives.</summary>
    private static async Task<IResult> PageAsync(
        HttpContext context, ChangeDecisionDesk decisions, EffectiveConfiguration configuration, IDictionary<string, object?> screen)
    {
        var inForce = configuration.Entries.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        screen[nameof(ConfigurationChangeScreen.Pending)] = await decisions.PendingAsync(context.RequestAborted);
        screen[nameof(ConfigurationChangeScreen.NotApplied)] = await NotAppliedAsync(context);
        screen[nameof(ConfigurationChangeScreen.Keys)] = new List<Field.Option>(
            ChangeableKeys.All.Select(key => new Field.Option(key, $"{ChangeTexts.Key(key)} (em vigor: {inForce.GetValueOrDefault(key) ?? "sem valor"})"))).AsReadOnly();
        return ScreenResult.Of(context, typeof(ConfigurationChangeScreen), ConfigurationChangeScreen.Title, screen);
    }

    /// <summary>The latest approved changes the core recorded as never to be applied; none when there is no database.</summary>
    private static async Task<IReadOnlyList<RefusedChange>> NotAppliedAsync(HttpContext context) =>
        context.RequestServices.GetService<ApprovedChanges>() is { } approved
            ? (await approved.RefusedAsync(context.RequestAborted)).Take(ShownNotApplied).ToList().AsReadOnly()
            : Array.Empty<RefusedChange>();

    private static long? ActorOf(HttpContext context) => context.RequestServices.GetRequiredService<SessionStore>().Resolve(SessionCookie.Read(context.Request));

    private static IResult Back(HttpContext context, string done)
    {
        AccountNotice.Issue(context.Response, done, 0);
        return PageRedirect.Of(context, ScreenPaths.ConfigurationChanges);
    }
}
