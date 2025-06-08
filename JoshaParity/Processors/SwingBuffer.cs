using JoshaParity.Data;
using JoshaParser.Data.Beatmap;

namespace JoshaParity.Processors;

/// <summary> Handles buffering notes and logic for determining when a swing is complete </summary>
public class SwingBuffer(float sliderPrecision = 59f, float maxSliderLength = float.MaxValue)
{
    private Dictionary<Hand, List<Note>> _buffers = new()
    {
        { Hand.Left, new() },
        { Hand.Right, new() }
    };

    public float SliderPrecision { get; private set; } = sliderPrecision;
    public float MaxSliderLength { get; private set; } = maxSliderLength;

    public SwingBuffer Clone()
    {
        return new SwingBuffer
        {
            _buffers = new Dictionary<Hand, List<Note>>
            {
                { Hand.Left, new List<Note>(_buffers[Hand.Left]) },
                { Hand.Right, new List<Note>(_buffers[Hand.Right]) }
            },
            SliderPrecision = this.SliderPrecision,
            MaxSliderLength = this.MaxSliderLength
        };
    }

    /// <summary> Appends a note to the buffer and finalizes the previous swing if needed. </summary>
    public List<Note>? Process(Note note)
    {
        Hand hand = note.C == 0 ? Hand.Left : Hand.Right;
        List<Note> buffer = _buffers[hand];

        bool isWithinMaxLength = buffer.Count == 0 || Math.Abs(note.MS - buffer.OrderBy(x => x.MS).First().MS) <= MaxSliderLength;

        if (buffer.Count == 0 || isWithinMaxLength && IsInGroup(buffer[buffer.Count - 1], note)) {
            buffer.Add(note);
            return null;
        }

        List<Note> finalized = [.. buffer];
        buffer.Clear();
        buffer.Add(note);
        return finalized;
    }

    /// <summary> Forces the flush of any remaining notes in the buffer for the given hand. </summary>
    public List<Note> ForceFlush(Hand hand)
    {
        List<Note> buffer = _buffers[hand];
        if (buffer.Count == 0) return [];

        List<Note> result = [.. buffer];
        buffer.Clear();
        return result;
    }

    /// <summary> Attempts to flush the buffer if no further valid groupings are expected. </summary>
    public List<Note>? TryFlushWithLookahead(Note nextNote)
    {
        Hand hand = nextNote.C == 0 ? Hand.Left : Hand.Right;
        List<Note> buffer = _buffers[hand];
        if (buffer.Count == 0) return null;

        bool isWithinMaxLength = Math.Abs(nextNote.MS - buffer.OrderBy(x => x.MS).First().MS) <= MaxSliderLength;

        if (!IsInGroup(buffer[buffer.Count - 1], nextNote) || !isWithinMaxLength) {
            List<Note> finalized = [.. buffer];
            buffer.Clear();
            return finalized;
        }

        return null;
    }

    public List<Note> GetBuffer(Hand hand) => _buffers[hand];

    /// <summary> Conditions for if this note can be apart of the current swing </summary>
    private bool IsInGroup(Note prev, Note next)
    {
        float prevEndMs = prev is Chain chain ? chain.TMS : prev.MS;
        float delta = next.MS - prevEndMs;
        return delta <= SliderPrecision && (prev.D == CutDirection.Any || next.D == CutDirection.Any || prev.D == next.D || prev.D.IsWithinIntervals(next.D, 1));
    }
}
