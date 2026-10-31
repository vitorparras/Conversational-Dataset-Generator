using Core.Persistence.Custody;
using Npgsql;

namespace Core.Persistence.People;

/// <summary>A statement that writes a person and the record of the change, with the way to bind it and the step of the chain of custody it is.</summary>
/// <param name="Sql">The statement; it returns the identifier of the person written, or nothing when it wrote nothing.</param>
/// <param name="Bind">Gives the parameters to the command.</param>
/// <param name="Step">The step the chain records when the statement wrote.</param>
/// <param name="PersonId">The person the statement changes, when it is known beforehand; null when the statement makes the person.</param>
internal sealed record PersonStatement(string Sql, Action<NpgsqlCommand> Bind, CustodyStep Step, long? PersonId = null);
