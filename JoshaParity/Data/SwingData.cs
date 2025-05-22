using System.Numerics;
using JoshaParity.Utils;
using JoshaParser.Data.Beatmap;

namespace JoshaParity.Data;

/// <summary> Represents which hand something belongs to </summary>
public enum Hand { Left, Right }

/// <summary> Represents a type of reset </summary>
public enum ResetType
{
    None = 0,      // Swing does not force a reset, or triangle
    Bomb = 1,      // Swing forces parity reset due to bombs
    Angle = 2,   // Swing forces parity reset due to extreme rotation
}

/// <summary> Represents a type of swing </summary>
public enum SwingType
{
    Unknown = 0,   // No standard classification for note composition
    Normal = 1,    // Singular note
    Stack = 2,     // 2 or 3 notes in a straight line on the same time snap
    Window = 3,    // Multiple notes on the same time snap with gaps
    Slider = 4,    // Multiple notes on differing time snaps
    Chain = 5,     // Chain note type
    DotSpam = 6,   // Dots spammed faster than the slider recognition for persisting time
    Blank = 99     // Swing type does not involve notes and represents any kind of inbetween motion
}

/// <summary> Represents the parity state of a swing </summary>
public enum Parity
{
    Undetermined = 0,      // Swing parity is undecided
    Forehand = 1,          // Swing wrist goes down
    Backhand = 2,          // Swing wrist goes up
}

/// <summary> Represents a swing completed by the bot </summary>
public sealed class SwingData(Hand hand, Parity parity, SwingType swingType, ResetType resetType, IReadOnlyList<SwingFrame> frames, IReadOnlyList<Note> notes, float swingEBPM)
{
    /// <summary> Which hand the swing belongs to </summary>
    public Hand Hand { get; } = hand;
    /// <summary> The parity of the swing </summary>
    public Parity Parity { get; } = parity;
    /// <summary> Swing composition </summary>
    public SwingType SwingType { get; } = swingType;
    /// <summary> Reset instigated during this swing </summary>
    public ResetType ResetType { get; } = resetType;
    /// <summary> Immutable list of motions for this swing </summary>
    public IReadOnlyList<SwingFrame> Frames { get; } = frames;
    /// <summary> Immutable list of notes for this swing </summary>
    public IReadOnlyList<Note> Notes { get; } = notes;
    /// <summary> Effective BPM speed of this swing </summary>
    public float SwingEBPM { get; } = swingEBPM;

    /// <summary> Returns the first motion frame of this swing </summary>
    public SwingFrame StartFrame => Frames[0];
    /// <summary> Returns the last motion frame of this swing </summary>
    public SwingFrame EndFrame => Frames[Frames.Count-1];
    /// <summary> Is this swing instigating a reset? </summary>
    public bool IsReset => ResetType != ResetType.None;
    /// <summary> Overview of swing information </summary>
    public override string ToString()
    {
        string framesSummary = Frames.Any()
            ? string.Join(", ", Frames.Select(f => $"({f.beats} beats, Pos: {f.x}x, {f.y}y, CutDirection: {f.dir}, Rot: {f.dir.ToRotation(this)})"))
            : "No Frames";

        string notesSummary = Notes?.Any() == true
            ? string.Join(", ", Notes.Select(f =>
                f is Chain chain
                    ? $"(Chain - {chain.B} beats, {chain.MS}ms, Start Pos: {chain.X}x, {chain.Y}y, End Pos: {chain.TX}x, {chain.TY}y, CutDirection: {chain.D}, SliceCount: {chain.SC}, SquishFactor: {chain.SF}, Color: {f.C})"
                    : $"(Note - {f.B} beats, {f.MS}ms, Pos: {f.X}x, {f.Y}y, CutDirection: {f.D}, Color: {f.C})"))
            : "No Notes";

        return $"Swing Note/s or Bomb/s at Start Beat: {StartFrame.beats} | Parity: {Parity} | Start Rotation: {StartFrame.dir.ToRotation(this)}\n" +
               $"Swing EBPM: {SwingEBPM} | Reset Type: {ResetType} | Swing Type: {SwingType} | Hand: {Hand}\n" +
               $"Frames: {framesSummary} | \nNotes: {notesSummary}";
    }
}

/// <summary> Builder used to construct a swing </summary>
public class SwingDataBuilder
{
    private Hand _hand = Hand.Left;
    private Parity _parity = Parity.Undetermined;
    private SwingType _swingType = SwingType.Blank;
    private ResetType _resetType = ResetType.None;
    private List<SwingFrame> _frames = [new(), new()];
    private List<Note> _notes = [];
    private float _swingEBPM = 0;

    public SwingDataBuilder WithHand(Hand hand) => Set(ref _hand, hand);
    public SwingDataBuilder WithParity(Parity parity) => Set(ref _parity, parity);
    public SwingDataBuilder WithSwingType(SwingType type) => Set(ref _swingType, type);
    public SwingDataBuilder WithResetType(ResetType type) => Set(ref _resetType, type);
    public SwingDataBuilder WithEBPM(float ebpm) => Set(ref _swingEBPM, ebpm);

    public SwingDataBuilder WithNotes(List<Note> notes) {
        _notes = notes?.ToList() ?? [];
        return this;
    }

    public SwingDataBuilder WithFrames(List<SwingFrame> frames) {
        _frames = frames?.ToList() ?? [];
        return this;
    }

    public SwingDataBuilder ReversePath() {
        _frames = _frames.FlipFrames();
        _notes.Reverse();
        return this;
    }

    public SwingDataBuilder PathSwing(SwingData? lastSwing = null) {
        List<List<Note>> sortedGroups = SwingUtils.SortNotes([.. _notes], lastSwing);
        List<SwingFrame> newFrames = [];
        Note? previousNote = null;
        _notes = sortedGroups.ToSingleList();

        // Special Checks - Special Snapped Windows
        if (_notes.Count == 2 && _notes.All(x => Math.Round(x.B, 3) == Math.Round(_notes[0].B, 3)))
        {
            var horDiff = Math.Abs(_notes[1].X - _notes[0].X);
            var verDiff = Math.Abs(_notes[1].Y - _notes[0].Y);
            if ((horDiff == 1 && verDiff == 2) || (horDiff == 2 && verDiff == 1))
            {
                List<SwingFrame> frames = [];
                Vector2 dir = new(_notes[1].X - _notes[0].X, _notes[1].Y - _notes[0].Y);
                foreach (Note note in _notes)
                {
                    frames.Add(new SwingFrame()
                    {
                        x = note.X,
                        y = note.Y,
                        beats = note.B,
                        dir = dir.NearestDiagonal()
                    });
                }
                _frames = frames;
                return this;
            }
        }

        // For each group of notes...
        for (int i = 0; i < sortedGroups.Count; i++)
        {
            List<Note> notesThisSnap = sortedGroups[i];
            Note? nextNote = null;
            if (i + 1 < sortedGroups.Count)
                nextNote = sortedGroups[i + 1].FirstOrDefault();

            // For each note in this time group...
            for (int j = 0; j < notesThisSnap.Count; j++)
            {
                Note currentNote = notesThisSnap[j];
                if (notesThisSnap.Count > j + 1) nextNote = notesThisSnap[j + 1];
                SwingFrame frame = new SwingFrame();
                frame.FromNote(currentNote);
                frame.dir = currentNote.D;

                if (currentNote.D == CutDirection.Any)
                {
                    Note referenceNote = (j == 0)
                        ? previousNote ?? (nextNote ?? currentNote)
                        : notesThisSnap[j - 1];

                    frame.dir = i == 0 && notesThisSnap.All(x => x.D == CutDirection.Any) && sortedGroups.Count > 1
                        ? SwingUtils.CutDirFromNoteToNote(currentNote, nextNote ?? currentNote)
                        : (previousNote == null && i == 0)
                            ? SwingUtils.CutDirFromNoteToNote(currentNote, referenceNote)
                            : SwingUtils.CutDirFromNoteToNote(referenceNote, currentNote);
                }

                if (_notes.Count == 1 && lastSwing != null && currentNote.D == CutDirection.Any)
                    frame.dir = SwingUtils.DotCutDirectionCalc(lastSwing, Build());

                newFrames.Add(frame);
                previousNote = currentNote;

                if (currentNote is Chain chain)
                    newFrames.Add(new SwingFrame()
                    {
                        x = chain.TX,
                        y = chain.TY,
                        beats = chain.TB,
                        dir = chain.D
                    });
            }
        }
        _frames = newFrames;
        return this;
    }

    private SwingDataBuilder Set<T>(ref T field, T value) {
        field = value;
        return this;
    }

    public SwingData Build() {
        if (_frames == null || _frames.Count == 0) throw new InvalidOperationException("Frames cannot be null or empty");
        return new SwingData(_hand, _parity, _swingType, _resetType, _frames, _notes, _swingEBPM);
    }
}