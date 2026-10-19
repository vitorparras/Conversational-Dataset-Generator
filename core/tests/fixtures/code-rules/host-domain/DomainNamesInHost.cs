using LabelSet = System.Collections.Generic.HashSet<string>;

namespace Core.Host.Annotation.Work;

internal sealed class ConversationSplitter
{
    private readonly int participants;

    public ConversationSplitter(int taxonomyVersion) => participants = taxonomyVersion;

    public string AnnotatorId { get; } = string.Empty;

    public int CountMessages(int participantCount) => participantCount + participants;
}

internal enum StageState
{
    Ingested,
    Exported,
}

internal static partial class AnnotationLogMessages
{
}

internal sealed record MessageOrder(int Position);

internal sealed class Queue<TComment>
{
    public TComment? Head { get; }
}

internal interface IExportSink
{
    void Flush();
}

internal sealed class CustodyTrail : IExportSink
{
    void IExportSink.Flush()
    {
    }
}
