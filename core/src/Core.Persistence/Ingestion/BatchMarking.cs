namespace Core.Persistence.Ingestion;

/// <summary>The mark of a place of the sample as reviewed: the place right after the last one marked, counted from 1 in the order of the sample.</summary>
public sealed record BatchMarking(int DomainId, long BatchId, int Place);
