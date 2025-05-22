using JoshaParity.Data;
using JoshaParser.Data.Beatmap;

namespace JoshaParity.Processors;

/// <summary> Handles buffering notes and logic for determining when a swing is complete </summary>
public class SwingBuffer
{
    private Dictionary<Hand, List<Note>> _buffers = new()
    {
        { Hand.Left, new() },
        { Hand.Right, new() }
    };

    public const float SliderPrecision = 59f;

    public SwingBuffer Clone()
    {
        return new SwingBuffer
        {
            _buffers = new Dictionary<Hand, List<Note>>
            {
                { Hand.Left, new List<Note>(_buffers[Hand.Left]) },
                { Hand.Right, new List<Note>(_buffers[Hand.Right]) }
            }
        };
    }

    /// <summary> Appends a note to the buffer and finalizes the previous swing if needed. </summary>
    public List<Note>? Process(Note note)
    {
        Hand hand = note.C == 0 ? Hand.Left : Hand.Right;
        var buffer = _buffers[hand];

        if (buffer.Count == 0 || IsInGroup(buffer[^1], note))
        {
            buffer.Add(note);
            return null;
        }

        List<Note> finalized = new(buffer);
        buffer.Clear();
        buffer.Add(note);
        return finalized;
    }

    /// <summary> Forces the flush of any remaining notes in the buffer for the given hand. </summary>
    public List<Note> ForceFlush(Hand hand)
    {
        var buffer = _buffers[hand];
        if (buffer.Count == 0) return [];

        var result = new List<Note>(buffer);
        buffer.Clear();
        return result;
    }

    /// <summary> Attempts to flush the buffer if no further valid groupings are expected. </summary>
    public List<Note>? TryFlushWithLookahead(Note nextNote)
    {
        var hand = nextNote.C == 0 ? Hand.Left : Hand.Right;
        var buffer = _buffers[hand];
        if (buffer.Count == 0) return null;

        if (!IsInGroup(buffer[^1], nextNote))
        {
            var finalized = new List<Note>(buffer);
            buffer.Clear();
            return finalized;
        }

        return null;
    }

    public List<Note> GetBuffer(Hand hand) => _buffers[hand];

    /// <summary> Conditions for if this note can be apart of the current swing </summary>
    private static bool IsInGroup(Note prev, Note next)
    {
        float prevEndMs = prev is Chain chain ? chain.TMS : prev.MS;
        float delta = next.MS - prevEndMs;
        if (delta > SliderPrecision) return false;
        return prev.D == CutDirection.Any || next.D == CutDirection.Any || prev.D == next.D || prev.D.IsWithinIntervals(next.D, 1);
    }
}
