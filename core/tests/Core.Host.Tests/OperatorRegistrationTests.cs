using System.Reflection;
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

        var (made, password) = await administration.RegisterAsync(1, new NewPerson("zqx-nome", "Auditoria"), string.Empty, TestContext.Current.CancellationToken);

        Assert.Equal((PersonOutcome.PasswordInvalid, "A senha não pode ser vazia."), (made.Outcome, made.Refusal));
        Assert.Null(password);
    }

    [Fact]
    public void ABlankFirstPasswordIsAnswered400AtThePasswordField()
    {
        var registered = OperatorAnswer.Registered(new PersonResult { Outcome = PersonOutcome.PasswordInvalid, Refusal = "A senha não pode ser vazia." }, null);

        Assert.Equal((null, 400, "senha", "A senha não pode ser vazia."), (registered.Id, registered.Status, registered.Refusal!.Field, registered.Refusal.Reason));
    }

    [Fact]
    public void APersonMadeWithThePasswordIsAnswered201WithTheIdentifierAndNoRefusal()
    {
        var registered = OperatorAnswer.Registered(new PersonResult { Outcome = PersonOutcome.Done, PersonId = 42 }, new PersonResult { Outcome = PersonOutcome.Done, PersonId = 42 });

        Assert.Equal((42, 201, null), (registered.Id, registered.Status, registered.Refusal));
    }

    [Fact]
    public void APersonMadeWhosePasswordWasNotSetIsAnswered500WithTheRouteThatSetsItAgain()
    {
        var registered = OperatorAnswer.Registered(new PersonResult { Outcome = PersonOutcome.Done, PersonId = 42 }, new PersonResult { Outcome = PersonOutcome.ChainBusy });

        Assert.Equal((null, 500, null), (registered.Id, registered.Status, registered.Refusal!.Field));
        Assert.Contains("PUT /people/42/password", registered.Refusal.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAdministrationOfOperatorsAnswersInTermsOfTheRegistryAndNothingOfTheRoutes()
    {
        var signatures = typeof(OperatorAdministration).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType))
            .SelectMany(Unwrapped);

        Assert.DoesNotContain(signatures, type => type.Namespace == typeof(OperatorAnswer).Namespace);
    }

    private static IEnumerable<Type> Unwrapped(Type type) =>
        type.IsGenericType ? type.GetGenericArguments().SelectMany(Unwrapped).Prepend(type) : [type];

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
