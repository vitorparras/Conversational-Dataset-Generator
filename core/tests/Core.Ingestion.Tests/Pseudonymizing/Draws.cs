using Core.Ingestion.Pseudonymizing;

namespace Core.Ingestion.Tests.Pseudonymizing;

/// <summary>Draws of markers for the tests: the real one, and one whose numbers come first from a list, so that a test can make a draw repeat a number it knows.</summary>
internal static class Draws
{
    /// <summary>A draw that gives the numbers listed, in order, and after them every number from zero up, so that a free number always comes.</summary>
    public static MarkerDraw Of(params int[] numbers)
    {
        var queue = new Queue<int>(numbers);
        var next = 0;
        return new MarkerDraw(() => queue.TryDequeue(out var number) ? number : next++);
    }

    /// <summary>The markers of a conversation, with the markers some other conversation already holds.</summary>
    public static ConversationMarkers Conversation(MarkerDraw draw, ICollection<Marker> inUse) => new(draw, inUse.Contains);
}
