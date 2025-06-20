﻿using JoshaParity.Data;
using JoshaParser.Data.Beatmap;

namespace JoshaParity.Processors;

/// <summary> Provides functionalities for classifying swings </summary>
public static class SwingClassifier
{
    /// <summary> Determines the type of swing given a list of notes. </summary>
    public static SwingType Classify(List<Note> swingNotes)
    {
        if (swingNotes.Count == 0)
            return SwingType.Unknown;

        if (swingNotes.Any(x => x is Chain))
            return SwingType.Chain;

        if (swingNotes.Count == 1)
            return SwingType.Normal;

        bool isStack = swingNotes.All(x => Math.Abs(swingNotes[0].B - x.B) < 0.01f);

        return isStack && IsStack(swingNotes)
            ? SwingType.Stack
            : isStack && IsWindow(swingNotes)
            ? SwingType.Window
            : swingNotes.Count >= 5 && swingNotes.All(x => x.D == CutDirection.Any) ? SwingType.DotSpam : SwingType.Slider;
    }
    /// <summary> Determines if notes compose a stack. </summary>
    private static bool IsStack(List<Note> notes)
    {
        Note lastNote = notes[0];
        for (int i = 1; i < notes.Count; i++) {
            Note nextNote = notes[i];
            if (Math.Abs(nextNote.X - lastNote.X) > 1 || Math.Abs(nextNote.Y - lastNote.Y) > 1)
                return false;
        }
        return true;
    }
    /// <summary> Determines if notes compose a window. </summary>
    private static bool IsWindow(List<Note> notes)
    {
        Note lastNote = notes[0];
        for (int i = 1; i < notes.Count; i++) {
            Note nextNote = notes[i];
            if (Math.Abs(nextNote.X - lastNote.X) > 1 || Math.Abs(nextNote.Y - lastNote.Y) > 1)
                return true;
        }
        return false;
    }
}
