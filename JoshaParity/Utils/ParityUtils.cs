using JoshaParity.Data;
using JoshaParser.Data.Beatmap;

namespace JoshaParity.Utils;

/// <summary> Parity-based utilities </summary>
public static class ParityUtils
{
    /// <summary> Cut Direction -> Angle from Neutral (up down 0 degrees) given a Right Forehand Swing </summary>
    private static readonly Dictionary<int, float> RightForehandDict = new()
        { { 0, -180 }, { 1, 0 }, { 2, -90 }, { 3, 90 }, { 4, -135 }, { 5, 135 }, { 6, -45 }, { 7, 45 }, { 8, 0 } };
    /// <summary>  Cut Direction -> Angle from Neutral (up down 0 degrees) given a Right Backhand Swing </summary>
    private static readonly Dictionary<int, float> RightBackhandDict = new()
        { { 0, 0 }, { 1, -180 }, { 2, 90 }, { 3, -90 }, { 4, 45 }, { 5, -45 }, { 6, 135 }, { 7, -135 }, { 8, 0 } };
    /// <summary> Cut Direction -> Angle from Neutral (up down 0 degrees) given a Left Forehand Swing </summary>
    private static readonly Dictionary<int, float> LeftForehandDict = new()
        { { 0, -180 }, { 1, 0 }, { 2, 90 }, { 3, -90 }, { 4, 135 }, { 5, -135 }, { 6, 45 }, { 7, -45 }, { 8, 0 } };
    /// <summary> Cut Direction -> Angle from Neutral (up down 0 degrees) given a Left Backhand Swing </summary>
    private static readonly Dictionary<int, float> LeftBackhandDict = new()
        { { 0, 0 }, { 1, -180 }, { 2, -90 }, { 3, 90 }, { 4, -45 }, { 5, 45 }, { 6, -135 }, { 7, 135 }, { 8, 0 } };
    /// <summary> Provides an initial parity, will be depreciated once branching paths function </summary>
    public static readonly Dictionary<int, Parity> InitialParity = new()
        { { 0, Parity.Backhand }, { 1, Parity.Forehand }, { 2, Parity.Forehand }, { 3,  Parity.Forehand }, { 4, Parity.Backhand },
        { 5, Parity.Backhand }, { 6, Parity.Forehand }, { 7, Parity.Forehand }, { 8, Parity.Forehand } };

    /// <summary> Short-hand returns the Forehand Parity Dictionary for a given hand </summary>
    public static Dictionary<int, float> ForehandDict(Hand hand) => hand == Hand.Right ? RightForehandDict : LeftForehandDict;

    /// <summary> Short-hand returns the Backhand Parity Dictionary for a given hand </summary>
    public static Dictionary<int, float> BackhandDict(Hand hand) => hand == Hand.Right ? RightBackhandDict : LeftBackhandDict;

    /// <summary> Performs an angle parity assessment to determine if reset is probable </summary>
    public static (ResetType, Parity) AssessParity(BotState state, SwingData nextSwing, List<BeatGridObject>? contextWindow = null)
    {
        SwingData lastSwing = nextSwing.Hand == Hand.Right ?
            state.GetAllSwings(Hand.Right).Last() :
            state.GetAllSwings(Hand.Left).Last();

        Note lastNote = lastSwing.Notes[lastSwing.Notes.Count - 1];
        Note nextNote = nextSwing.Notes[0];
        CutDirection lastCutDir = lastSwing.EndFrame.dir;
        CutDirection nextCutDir = nextSwing.Notes.All(x => x.D == CutDirection.Any)
            ? SwingUtils.CutDirFromNoteToNote(lastNote, nextNote)
            : nextSwing.Notes.First(x => x.D != CutDirection.Any).D;

        float lastAFN = lastCutDir.ToRotation(lastSwing.Parity == Parity.Forehand ? Parity.Forehand : Parity.Backhand, lastSwing.Hand);
        float nextAFN = nextCutDir.ToRotation(lastSwing.Parity == Parity.Forehand ? Parity.Backhand : Parity.Forehand, lastSwing.Hand);
        float AFNChange = lastAFN - nextAFN;

        // TO DO: Implement bomb check logic based on current position and contextWindow
        if (nextSwing.Notes.All(x => x.D == CutDirection.Any))
            return (lastSwing.Parity == Parity.Forehand) ? (ResetType.None, Parity.Backhand) : (ResetType.None, Parity.Forehand);

        return (Math.Abs(AFNChange) > state.Config.AngleTolerance || Math.Abs(nextAFN) > state.Config.AngleLimit)
            ? (lastSwing.Parity == Parity.Forehand) ? (ResetType.Angle, Parity.Forehand) : (ResetType.Angle, Parity.Backhand)
            : (lastSwing.Parity == Parity.Forehand) ? (ResetType.None, Parity.Backhand) : (ResetType.None, Parity.Forehand);
    }

    /// <summary> Converts an angle in AFN to a CutDirection based on the given hand and parity </summary>
    public static CutDirection GetCutDirectionFromAFN(float angleAFN, Hand hand, Parity parity)
    {
        Dictionary<int, float> relevantDict = parity == Parity.Forehand ? ForehandDict(hand) : BackhandDict(hand);
        KeyValuePair<int, float> bestMatch = relevantDict
            .OrderBy(kvp => Math.Abs(kvp.Value - angleAFN))
            .First();
        return (CutDirection)bestMatch.Key;
    }
}
