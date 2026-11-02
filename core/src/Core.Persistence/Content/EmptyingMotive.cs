namespace Core.Persistence.Content;

/// <summary>Why an emptying is asked for; the list lives here only, so that no free text reaches the chain.</summary>
public enum EmptyingMotive
{
    /// <summary>The person the content is about asked for it to be removed.</summary>
    RemovalRequestedBySubject,

    /// <summary>The contract with whoever handed the content over requires its removal.</summary>
    ContractRequirement,
}
