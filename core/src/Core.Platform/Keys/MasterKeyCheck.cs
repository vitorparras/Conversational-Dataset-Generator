using System.Diagnostics;
using Core.Platform.Startup;

namespace Core.Platform.Keys;

/// <summary>Refuses to start without a usable master key, and says what is missing or what was found, never the value.</summary>
public sealed class MasterKeyCheck : IStartupCheck
{
    private readonly IMasterKeySource source;

    /// <summary>The check reads the key through the source.</summary>
    public MasterKeyCheck(IMasterKeySource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        this.source = source;
    }

    /// <inheritdoc />
    public string Name => "MasterKey";

    /// <inheritdoc />
    public async Task<CheckResult> RunAsync(CancellationToken cancellationToken)
    {
        using var reading = await source.ReadAsync(cancellationToken).ConfigureAwait(false);
        return reading.Failure is { } failure
            ? CheckResult.Failed(Name, Describe(failure), source.Origin)
            : CheckResult.Passed(Name, $"chave-mestra de {MasterKey.Length} bytes lida", source.Origin);
    }

    private static string Describe(MasterKeyFailure failure) => failure.Kind switch
    {
        MasterKeyFailureKind.Absent => $"chave-mestra ausente: {failure.Found}; ela é fornecida na partida e não tem valor padrão",
        MasterKeyFailureKind.Unreadable => $"chave-mestra ilegível: encontrado {failure.Found}",
        MasterKeyFailureKind.WrongSize => $"chave-mestra de tamanho inválido: encontrado {failure.Found}",
        _ => throw new UnreachableException(),
    };
}
