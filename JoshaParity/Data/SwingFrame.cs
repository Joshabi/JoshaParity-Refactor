using JoshaParity.Utils;
using JoshaParser.Data.Beatmap;

namespace JoshaParity.Data;

/// <summary> Represents a point during a swings motion </summary>
public class SwingFrame
{
    public float beats = 0;
    public float x = 0;
    public float y = 0;
    public CutDirection dir = CutDirection.Any;
}

/// <summary> Extensions for Swing Frames </summary>
public static class SwingFrameExtensions
{
    /// <summary> Configures the frame based on a given note </summary>
    public static void FromNote(this SwingFrame frame, Note note)
    {
        frame.beats = note.B;
        frame.x = note.X;
        frame.y = note.Y;
        frame.dir = note.D;
    }

    /// <summary> Flips swing frames </summary>
    public static List<SwingFrame> FlipFrames(this List<SwingFrame> frames)
    {
        if (frames == null || frames.Count == 0)
            return [];

        int frameCount = frames.Count;
        List<SwingFrame> flippedFrames = new(frameCount);

        for (int i = 0; i < frameCount; i++)
        {
            SwingFrame originalFrame = frames[i];
            SwingFrame mirrorFrame = frames[frameCount - i - 1];
            SwingFrame flippedFrame = new()
            {
                x = mirrorFrame.x,
                y = mirrorFrame.y,
                dir = originalFrame.dir != CutDirection.Any
                    ? SwingUtils.OpposingCutDict[originalFrame.dir]
                    : CutDirection.Any,
                beats = originalFrame.beats
            };

            flippedFrames.Add(flippedFrame);
        }

        return flippedFrames;
    }
}