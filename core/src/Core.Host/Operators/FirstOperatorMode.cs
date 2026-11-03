using Core.Persistence.Connection;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Host.Operators;

/// <summary>
/// The mode the operator asks for with <c>create-person --name &lt;name&gt; --function &lt;function&gt;</c> as the first arguments (issue 65): the same executable, run once with the application's
/// credential, never the owner's, makes the first person of the installation when the registry has nobody, and sets the password read from the standard input. It serves nothing, takes no
/// password as an argument, and says what it did without the name, the password or the credential in the words.
/// </summary>
internal static class FirstOperatorMode
{
    private const string Argument = "create-person";
    private const string Prefix = "create-person recusado";

    internal static bool Requested(string[] args) => args is [Argument, ..];

    internal static async Task<HostLaunch> RunAsync(string[] args, TextReader input, TextWriter output, TextWriter error)
    {
        var asked = FirstOperatorArguments.Split(args);
        if (ArgumentsProblem(asked) is { } problem)
        {
            await error.WriteLineAsync($"{Prefix}: {problem}").ConfigureAwait(false);
            return HostLaunch.Ended(ExitCode.ConfigurationRefused);
        }

        var loaded = HostConfiguration.Load(asked.Remaining, migrating: true);
        var database = HostConfiguration.DatabaseOf(loaded, migrating: true);
        if (database is null || database.State == DatabaseSettingsState.Refused)
        {
            return HostLaunch.Refused(database?.Problems ?? loaded.Problems);
        }

        if (PasswordConfiguration.RefusalsWhenRequired(loaded.Values) is { Count: > 0 } invalid)
        {
            return HostLaunch.Refused(invalid);
        }

        var password = await input.ReadLineAsync().ConfigureAwait(false) ?? string.Empty;
        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString!);
        var first = new FirstOperator(asked.Name!, asked.Function!, password);
        var exit = await CreateAsync(dataSource, PasswordConfiguration.Derivation(loaded.Values), first, new Words(output, error)).ConfigureAwait(false);
        return HostLaunch.Ended(exit);
    }

    private static string? ArgumentsProblem(FirstOperatorArguments asked)
    {
        if (asked.PasswordOffered)
        {
            return "a senha não vai por argumento; envie-a pela entrada padrão";
        }

        return asked.Name is null || asked.Function is null ? "informe o nome e a função, uma vez cada: --name <nome> --function <função>" : null;
    }

    /// <summary>The creation itself, over a source that was made: a field not accepted refuses before the database is touched, and the password is written only as the derived credential.</summary>
    internal static async Task<ExitCode> CreateAsync(NpgsqlDataSource dataSource, PasswordDerivation derivation, FirstOperator first, Words words)
    {
        var fields = FieldsRefused(first).ToList();
        foreach (var field in fields)
        {
            await words.Error.WriteLineAsync($"{Prefix}, nada foi gravado: {field}").ConfigureAwait(false);
        }

        if (fields.Count > 0)
        {
            return ExitCode.PersonCreationRefused;
        }

        try
        {
            return await WriteAsync(dataSource, derivation, first, words).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            await words.Error.WriteLineAsync($"{Prefix}, {WhatWasWritten(first.Id)}: o servidor recusou a gravação").ConfigureAwait(false);
            return ExitCode.PersonCreationRefused;
        }
        catch (NpgsqlException)
        {
            await words.Error.WriteLineAsync($"create-person falhou ao falar com o banco, {WhatWasWritten(first.Id)}").ConfigureAwait(false);
            return ExitCode.PersonCreationFailed;
        }
    }

    private static async Task<ExitCode> WriteAsync(NpgsqlDataSource dataSource, PasswordDerivation derivation, FirstOperator first, Words words)
    {
        var holdsTheSchema = await OwnerCredentialCheck.HoldsTheSchemaAsync(dataSource, CancellationToken.None).ConfigureAwait(false);
        if (holdsTheSchema is not false)
        {
            await words.Error.WriteLineAsync(holdsTheSchema is null
                ? $"{Prefix}, nada foi gravado: o banco não tem o cadastro; rode a migração antes"
                : $"{Prefix}, nada foi gravado: a credencial é a de dono do banco, que só migra; use a da aplicação").ConfigureAwait(false);
            return ExitCode.PersonCreationRefused;
        }

        var made = await new PersonRegistry(dataSource).CreateFirstAsync(new NewPerson(first.Name, first.Function), CancellationToken.None).ConfigureAwait(false);
        if (made.PersonId is not { } id)
        {
            await words.Error.WriteLineAsync($"{Prefix}, nada foi gravado: {made.Refusal}").ConfigureAwait(false);
            return made.Outcome == PersonOutcome.RegistryNotEmpty ? ExitCode.PersonRegistryNotEmpty : ExitCode.PersonCreationRefused;
        }

        first.Id = id;
        return await SetPasswordAsync(dataSource, derivation, first, words).ConfigureAwait(false);
    }

    private static async Task<ExitCode> SetPasswordAsync(NpgsqlDataSource dataSource, PasswordDerivation derivation, FirstOperator first, Words words)
    {
        var id = first.Id!.Value;
        var credentials = CompositionRoot.CredentialsOf(dataSource, derivation);
        var set = await credentials.SetPasswordAsync(id, first.Password, CustodyAuthor.System, CancellationToken.None).ConfigureAwait(false);
        if (set.Outcome != PersonOutcome.Done)
        {
            await words.Error.WriteLineAsync($"{Prefix}: a pessoa foi criada com o id {id} e a senha não foi definida: {set.Refusal}").ConfigureAwait(false);
            return ExitCode.PersonCreationFailed;
        }

        await words.Output.WriteLineAsync($"create-person: pessoa criada com o id {id}, com a senha definida").ConfigureAwait(false);
        return ExitCode.Success;
    }

    private static IEnumerable<string> FieldsRefused(FirstOperator first)
    {
        if (string.IsNullOrEmpty(first.Name) || first.Name != first.Name.Trim())
        {
            yield return "o campo 'nome' não pode ser vazio nem ter espaço nas pontas";
        }

        if (!PersonFunctions.IsAccepted(first.Function))
        {
            yield return $"o campo 'função' não consta da política de acesso; as aceitas são: {string.Join(", ", PersonFunctions.Accepted)}";
        }

        if (first.Password.Length == 0)
        {
            yield return "o campo 'senha' não pode ser vazio; envie-a pela entrada padrão";
        }
    }

    private static string WhatWasWritten(long? created) =>
        created is { } id ? $"a pessoa foi criada com o id {id} e a senha não foi definida" : "nada foi gravado";

    /// <summary>Where the mode says what it did: the output for the success and the error for every refusal.</summary>
    internal readonly record struct Words(TextWriter Output, TextWriter Error);
}
