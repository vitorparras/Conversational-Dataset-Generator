using System.Runtime.CompilerServices;
using Core.Persistence.Content;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Chain.Append;
using Npgsql;

namespace Core.Persistence.Ingestion;

/// <summary>The reading of the text of an item of the sample while the batch is open (issue 822): a door of the database that answers only for an item the sample drew and only after the event of the reading was written in the same transaction; when anything after the event refuses, nothing is recorded.</summary>
public sealed class SampleTextReader
{
    private readonly NpgsqlDataSource dataSource;
    private readonly ProtectedContent protectedContent;

    /// <summary>Reads over the database of the application, opening the text with the keys the host holds.</summary>
    /// <param name="dataSource">The data source with the credential of the application.</param>
    /// <param name="protectedContent">The protected content that opens the sealed text.</param>
    public SampleTextReader(NpgsqlDataSource dataSource, ProtectedContent protectedContent)
    {
        this.dataSource = dataSource;
        this.protectedContent = protectedContent;
    }

    /// <summary>Reads the text of the item of the sample, recording who read it before the text is opened.</summary>
    /// <param name="request">The batch, the item of its sample and the person who reads.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    public Task<ContentReadResult> ReadAsync(SampleReading request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.DomainId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.BatchId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.ItemId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.ReaderId);
        return BatchRetry.RunAsync(() => ReadingAsync(request, cancellationToken), result => result.Refusal?.Outcome == CustodyOutcome.Conflict, BatchRetry.Attempts, cancellationToken);
    }

    private async Task<CustodyResult?> ReadInAsync(CustodyScope scope, SampleReading request, StrongBox<ContentReadResult> answer, CancellationToken cancellationToken)
    {
        await ReviewStatements.DeclareDomainAsync(scope, request.DomainId, cancellationToken).ConfigureAwait(false);
        var step = await SampleTextStatements.StepAsync(scope, request, cancellationToken).ConfigureAwait(false);
        if (step is null)
        {
            answer.Value = new ContentReadResult { Outcome = ContentReadOutcome.NotFound, RowId = request.ItemId };
            return null;
        }

        var sanitized = step == CustodyNames.Of(CustodyStep.SanitizedTextRead);
        var item = new CustodyItem(CustodyItemKind.ItemText, request.ItemId);
        var custodyStep = sanitized ? CustodyStep.SanitizedTextRead : CustodyStep.RawTextRead;
        var appended = await CustodyAppend.AppendAsync(
            scope, CustodyEvent.Of(CustodyAuthor.Person(request.ReaderId), item, custodyStep, CustodyReason.ContentRead), cancellationToken).ConfigureAwait(false);
        if (appended.Outcome != CustodyOutcome.Appended)
        {
            answer.Value = new ContentReadResult { Outcome = ContentReadOutcome.NotRecorded, Refusal = appended, RowId = request.ItemId };
            return appended;
        }

        return await OpenedAsync(scope, request, (appended, sanitized ? ContentColumns.SanitizedText : ContentColumns.RawText), answer, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CustodyResult?> OpenedAsync(
        CustodyScope scope, SampleReading request, (CustodyResult Appended, ProtectedColumn Column) read, StrongBox<ContentReadResult> answer, CancellationToken cancellationToken)
    {
        var stored = await SampleTextStatements.SealedAsync(scope, request, cancellationToken).ConfigureAwait(false);
        if (stored is not { } held)
        {
            answer.Value = new ContentReadResult { Outcome = ContentReadOutcome.NotFound, RowId = request.ItemId };
            return null;
        }

        var lost = await DeclaredKeyLoss.ScopesAsync(dataSource, cancellationToken).ConfigureAwait(false);
        var carried = ProtectedContent.LabelsCarried(held.Sealed, held.Key);
        var scopeLost = lost.Contains(KeyLossScope.MasterText) ? KeyLossScope.MasterText : carried.FirstOrDefault(lost.Contains);
        if (scopeLost is not null)
        {
            answer.Value = new ContentReadResult { Outcome = ContentReadOutcome.KeyLossDeclared, Label = scopeLost, RowId = request.ItemId };
            return null;
        }

        var missing = carried.FirstOrDefault(label => !protectedContent.IsLoaded(label));
        if (missing is not null)
        {
            answer.Value = new ContentReadResult { Outcome = ContentReadOutcome.LabelMissing, Label = missing, RowId = request.ItemId };
            return null;
        }

        var text = protectedContent.Open(read.Column, request.DomainId, held.Sealed, held.Key);
        answer.Value = new ContentReadResult { Outcome = ContentReadOutcome.Read, Text = text, Cleaned = held.Step == CustodyNames.Of(CustodyStep.SanitizedTextRead), RowId = request.ItemId };
        return await CustodyAppend.CommitIfAppendedAsync(scope, read.Appended, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ContentReadResult> ReadingAsync(SampleReading request, CancellationToken cancellationToken)
    {
        var answer = new StrongBox<ContentReadResult>(new ContentReadResult { Outcome = ContentReadOutcome.NotFound });
        var recorded = await CustodyTransaction.RunAsync(dataSource, scope => ReadInAsync(scope, request, answer, cancellationToken), cancellationToken).ConfigureAwait(false);
        return recorded?.Outcome == CustodyOutcome.Conflict ? new ContentReadResult { Outcome = ContentReadOutcome.NotRecorded, Refusal = recorded, RowId = request.ItemId } : answer.Value!;
    }
}
