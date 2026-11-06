using Core.Host.Endpoints;
using Core.Host.Operators;
using Core.Persistence.People;
using Core.Platform.Sessions;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>
/// The registration of a person as the route and the screen share it (#599): the first password is required before anything is asked of the registry, and a refusal is told at the field the form
/// has. The registry itself is the subject of the tests of the database.
/// </summary>
public sealed class OperatorRegistrationTests
{
    [Fact]
    public async Task ABlankFirstPasswordIsRefusedAtThePasswordFieldBeforeAnythingIsAskedOfTheRegistry()
    {
        await using var source = NpgsqlDataSource.Create(UnreachableDatabase.ConnectionString);
        var sessions = new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System);
        var administration = new OperatorAdministration(new PersonRegistry(source), new PersonCredentials(source, new PasswordDerivation(2)), sessions);

        var registered = await administration.RegisterAsync(1, new OperatorAsked("zqx-nome", "Auditoria", string.Empty), TestContext.Current.CancellationToken);

        Assert.Null(registered.Id);
        Assert.Equal(("senha", 400), (registered.Refusal!.Field, registered.Status));
        Assert.Equal("A senha não pode ser vazia.", registered.Refusal.Reason);
    }

    [Theory]
    [InlineData(PersonOutcome.NameInvalid, "nome")]
    [InlineData(PersonOutcome.NameTaken, "nome")]
    [InlineData(PersonOutcome.FunctionNotAccepted, "função")]
    [InlineData(PersonOutcome.PasswordInvalid, "senha")]
    [InlineData(PersonOutcome.ChainBusy, null)]
    [InlineData(PersonOutcome.ActorDeactivated, null)]
    public void TheFieldOfARefusalIsTheOneThePolicyNamesAndNoneWhenTheRefusalIsAboutNoField(PersonOutcome outcome, string? field) =>
        Assert.Equal(field, OperatorAnswer.FieldOf(outcome));

    [Fact]
    public void ANameTakenIsAnswered409AtTheNameFieldWithTheReasonOfTheRegistry()
    {
        var (refusal, status) = OperatorAnswer.RefusalOf(new PersonResult { Outcome = PersonOutcome.NameTaken, Refusal = "O nome já está em uso." });

        Assert.Equal((409, "nome", "O nome já está em uso."), (status, refusal.Field, refusal.Reason));
    }
}
