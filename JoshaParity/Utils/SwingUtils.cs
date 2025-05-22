using System.Numerics;
using JoshaParity.Data;
using JoshaParser.Data.Beatmap;

namespace JoshaParity.Utils;

/// <summary> Swing Creation and Manipulation utilities </summary>
public static class SwingUtils
{
    /// <summary> Takes a Cut Direction (key) and returns the opposing Cut Direction (value) </summary>
    public static readonly Dictionary<CutDirection, CutDirection> OpposingCutDict = new()
    {
        { CutDirection.Up, CutDirection.Down },
        { CutDirection.Down, CutDirection.Up },
        { CutDirection.Left, CutDirection.Right },
        { CutDirection.Right, CutDirection.Left },
        { CutDirection.UpLeft, CutDirection.DownRight },
        { CutDirection.UpRight, CutDirection.DownLeft },
        { CutDirection.DownLeft, CutDirection.UpRight },
        { CutDirection.DownRight, CutDirection.UpLeft },
        { CutDirection.Any, CutDirection.Any }
    };

    /// <summary> Cut Direction (as index) to Directional Vector (not normalized) </summary>
    public static readonly Vector2[] DirectionalVectors =
    {
        new(0, 1),   // up
        new(0, -1),  // down
        new(-1, 0),  // left
        new(1, 0),   // right
        new(-1, 1),   // up left
        new(1, 1),  // up right
        new(-1, -1), // down left
        new(1, -1)  // down right
    };

    /// <summary> Converts Directional Vector (not normalized, key) to Cut Direction (value)</summary>
    public static readonly Dictionary<Vector2, CutDirection> DirectionalVectorToCutDirection = new()
    {
        { new Vector2(0, 1), CutDirection.Up },
        { new Vector2(0, -1), CutDirection.Down },
        { new Vector2(-1, 0), CutDirection.Left },
        { new Vector2(1, 0), CutDirection.Right },
        { new Vector2(-1, 1), CutDirection.UpLeft },
        { new Vector2(1, 1), CutDirection.UpRight },
        { new Vector2(-1, -1), CutDirection.DownLeft },
        { new Vector2(1, -1), CutDirection.DownRight },
        { new Vector2(0, 0), CutDirection.Any }
    };

    /// <summary> Given a Vector2 Direction, calculate a Cut Direction </summary>
    public static CutDirection CutDirFromVector(this Vector2 direction)
    {
        if (direction.LengthSquared() == 0) return CutDirection.Any;  // No movement
        Vector2 bestMatch = SwingUtils.DirectionalVectors.OrderByDescending(v => Vector2.Dot(Vector2.Normalize(direction), Vector2.Normalize(v))).First();
        Vector2 cutDirectionVector = new((float)Math.Round(bestMatch.X), (float)Math.Round(bestMatch.Y));
        return SwingUtils.DirectionalVectorToCutDirection[cutDirectionVector];
    }

    /// <summary> Given 2 notes, calculate a Cut Direction ID for the direction from first to last note </summary>
    public static CutDirection CutDirFromNoteToNote(Note firstNote, Note lastNote)
    { return CutDirFromVector(new Vector2(lastNote.X, lastNote.Y) - new Vector2(firstNote.X, firstNote.Y)); }

    /// <summary> Returns the 2 notes furthest apart in a list </summary>
    public static (Note NoteA, Note NoteB) GetFurthestApartNotes(this List<Note> notes)
    {
        if (notes.Count < 2)
            return (notes[0], notes[0]);

        var furthestPair = notes
            .SelectMany((note1, i) => notes.Skip(i + 1), (note1, note2) => new
            {
                Note1 = note1,
                Note2 = note2,
                DistanceSquared = Math.Pow(note2.X - note1.X, 2) + Math.Pow(note2.Y - note1.Y, 2)
            })
            .OrderByDescending(pair => pair.DistanceSquared)
            .First();

        return (furthestPair.Note1, furthestPair.Note2);
    }

    /// THE BELOW SHOULD BE REVIEWED FOR EFFIENCY
    /// THE CODE WAS HASTILY THROWN TOGETHER WHEN IMPLEMENTED LAST TIME

    /// <summary> Calculates a swing order grouped by time snap </summary>
    public static List<List<Note>> SortNotes(List<Note> notes, SwingData? lastSwing)
    {
        List<Note> sortedByTime = [.. notes.OrderBy(note => note.B)];
        Note? lastNoteLastSwing = lastSwing?.Notes.Last() ?? null;
        List<List<Note>> sortedGroups = [];

        // Group notes by time snap then iterate
        var groupedByTime = sortedByTime.GroupBy(note => Math.Round(note.B, 3)).ToList();
        for (int i = 0; i < groupedByTime.Count; i++)
        {
            List<Note> notesThisSnap = [.. groupedByTime[i]];
            if (notesThisSnap.Count > 1)
            {
                Note? nextNote = null;
                if (i + 1 < groupedByTime.Count)
                    nextNote = groupedByTime[i + 1].FirstOrDefault();

                if (notesThisSnap.Count > 1)
                    notesThisSnap = SnappedSwingSort(notesThisSnap, lastNoteLastSwing, nextNote);
            }
            sortedGroups.Add(notesThisSnap);
        }
        return sortedGroups;
    }

    /// <summary> Merges grouped notes down to a single list in order </summary>
    public static List<Note> ToSingleList(this List<List<Note>> groups) { return groups.SelectMany(group => group).ToList(); }

    /// <summary> Orders notes on the same time snap by average direction or vector between 2 furthest notes (dots) </summary>
    public static List<Note> SnappedSwingSort(List<Note> notesToSort, Note? lastNote = null, Note? nextNote = null)
    {
        if (notesToSort.Any(x => x.D != CutDirection.Any))
        {
            // Calculate average direction from all arrowed notes present
            Vector2 totalDirection = Vector2.Zero;
            foreach (Note note in notesToSort)
            {
                if (note.D == CutDirection.Any) continue;
                totalDirection += DirectionalVectors[(int)note.D];
            }

            // Sort notes based on projection along the average direction
            Vector2 avgDirection = totalDirection / (notesToSort.Count - notesToSort.Count(x => x.D == CutDirection.Any));
            return [.. notesToSort.OrderBy(x => Vector2.Dot(new Vector2(x.X, x.Y), avgDirection))];
        }

        // Handles cases where all notes are dots
        // Find two furthest apart notes and establish a cut direction
        (Note noteA, Note noteB) = notesToSort.GetFurthestApartNotes();
        Vector2 noteAPos = new Vector2(noteA.X, noteA.Y);
        Vector2 noteBPos = new Vector2(noteB.X, noteB.Y);
        Vector2 atb = noteBPos - noteAPos;

        // Normalize the direction vector between noteA and noteB
        if (atb != Vector2.Zero)
            atb = Vector2.Normalize(atb);

        // Determine if we need to reverse the order based on the lastNote or nextNote
        bool shouldReverse = false;
        if (lastNote != null)
        {
            Vector2 lastNotePos = new(lastNote.X, lastNote.Y);
            Vector2 lastToFirst = noteAPos - lastNotePos;

            if (lastToFirst != Vector2.Zero)
                lastToFirst = Vector2.Normalize(lastToFirst);

            double angle = Math.Acos(Clamp(Vector2.Dot(lastToFirst, atb), -1.0f, 1.0f));

            // If the angle is greater than 90 degrees, we might need to reverse
            if (angle > Math.PI / 2 && angle < Math.PI)
                shouldReverse = true;
        }
        else if (nextNote != null)
        {
            Vector2 nextNotePos = new(nextNote.X, nextNote.Y);
            Vector2 lastToFirst = nextNotePos - noteBPos;

            if (lastToFirst != Vector2.Zero)
                lastToFirst = Vector2.Normalize(lastToFirst);

            double angle = Math.Acos(SwingUtils.Clamp(Vector2.Dot(lastToFirst, atb), -1.0f, 1.0f));

            if (angle > Math.PI / 2)
                shouldReverse = true;
        }

        // Order notes based on projection along the established direction vector
        List<Note> sortedNotes = [.. notesToSort.OrderBy(x => Vector2.Dot(new Vector2(x.X, x.Y), atb))];

        if (shouldReverse)
            sortedNotes.Reverse();

        return sortedNotes;
    }

    /// <summary> Calculates a cut direction for a dot given a swing and the prior swing </summary>
    public static CutDirection DotCutDirectionCalc(SwingData lastSwing, SwingData nextSwing)
    {
        Note nextNote = nextSwing.Notes[0];
        Note lastNote = lastSwing.Notes[lastSwing.Notes.Count - 1];

        // If same grid position, just maintain angle
        CutDirection lastDir = OpposingCutDict[lastSwing.EndFrame.dir];
        CutDirection noteToNoteDir = CutDirFromNoteToNote(lastNote, nextNote);

        float distanceToNote = Vector2.Distance(new(lastNote.X, lastNote.Y), new(nextNote.X, nextNote.Y));
        return nextSwing.IsReset
            ? lastSwing.EndFrame.dir : nextNote.X == lastNote.X && nextNote.Y == lastNote.Y
            ? lastDir : distanceToNote > 1.42
            ? lastDir.MidwayTo(noteToNoteDir, true) : lastDir.MidwayTo(noteToNoteDir, false);
    }

    /// <summary> Clamp a value between a minimum and maximum </summary>
    public static float Clamp(float value, float min, float max)
    {
        return value < min ? min : value > max ? max : value;
    }
}