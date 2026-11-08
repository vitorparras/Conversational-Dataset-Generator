using System.Diagnostics.CodeAnalysis;
using System.Net;
using Core.Host.Operators;
using Core.Persistence.People;
using Core.Platform.Sessions;
using Npgsql;

namespace Core.Host.Tests.Operators;

/// <summary>
/// What the change of the own password decides before the store of credentials answers (issue 597): an empty new password is refused and counts nothing, and a full limit of the entry refuses
/// with the answer of any refusal, without the current password being checked, counting in the delay of the entry. The database here never answers: reaching it is seen from the outside.
/// </summary>
public sealed class PasswordChangeTests
{
    private static readonly IPAddress Here = SignInDelayTests.NetworkNumber(1);

    [Fact]
    public async Task AnEmptyNewPasswordIsRefusedBeforeAnythingIsCheckedOrCounted()
    {
        var delay = new SignInDelay(new ManualClock());
        using var database = new SignInSaturationTests.SilentDatabase();
        var change = Change(database.ConnectionString, delay);

        var result = await change.ChangeAsync(new PasswordChangeRequest(7, "session", Here), "the current one", string.Empty, TestContext.Current.CancellationToken);

        Assert.Equal((PasswordChangeOutcome.EmptyReplacement, 0), (result.Outcome, database.Connections));
        Assert.True(delay.RefusedAsync("someone", Here, TestContext.Current.CancellationToken).IsCompleted);
    }

    [Fact]
    public async Task WithNoPlaceLeftInTheLimitsOfTheEntryTheChangeIsRefusedWithoutCheckingThePasswordAndCountsInTheDelay()
    {
        var delay = new SignInDelay(new ManualClock());
        var places = Enumerable.Range(0, SignInDelay.MaxWaitingPerOrigin).Select(_ => delay.Admit(Here)).ToList();
        using var database = new SignInSaturationTests.SilentDatabase();
        var change = Change(database.ConnectionString, delay);

        var result = await change.ChangeAsync(new PasswordChangeRequest(7, "session", Here), "the current one", "the new one", TestContext.Current.CancellationToken);

        Assert.Equal((PasswordChangeOutcome.Refused, 0), (result.Outcome, database.Connections));
        Assert.False(delay.RefusedAsync("someone", Here, TestContext.Current.CancellationToken).IsCompleted);
        places.ForEach(place => place!.Dispose());
    }

    [SuppressMessage("Reliability", "CA2000", Justification = "The data source never connects to anything real, so it holds nothing to release.")]
    private static PasswordChange Change(string connectionString, SignInDelay delay)
    {
        var source = NpgsqlDataSource.Create(connectionString);
        var sessions = new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System);
        return new PasswordChange(new PersonCredentials(source, new PasswordDerivation(1)), sessions, delay);
    }
}
