using JoshaParser.Data.Beatmap;

namespace JoshaParity.Utils;

/// <summary> Time and BPM-based utilities </summary>
public class TimeUtils
{
    /// <summary> Returns the effective BPM of a swing given two points in beats </summary>
    public static float SwingEBPM(BPMContext bpmContext, float startBeat, float endBeat)
    { return SwingEBPM(bpmContext.ToRealTime(startBeat), bpmContext.ToRealTime(endBeat)); }

    /// <summary> Returns the effective BPM of a swing given two points in time </summary>
    public static float SwingEBPM(float startTime, float endTime)
    {
        if (startTime == endTime) { return 0; }
        float secondsDiff = endTime - startTime;
        return (float)(60 / (2 * secondsDiff));
    }

    /// <summary> Returns in seconds the real time between 2 points given in beats </summary>
    public static float BeatsToSeconds(BPMContext bpmContext, float startBeat, float endBeat)
    { return startBeat == 0 && endBeat == 0 ? 0 : bpmContext.ToRealTime(endBeat) - bpmContext.ToRealTime(startBeat); }
}