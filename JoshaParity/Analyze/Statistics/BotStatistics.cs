using JoshaParity.Data;
using JoshaParity.Utils;
using JoshaParser.Data.Metadata;
using System.Numerics;

namespace JoshaParity.Analyze.Statistics;

public delegate IEnumerable<double> SwingSelector(IEnumerable<SwingData> swings);
public delegate IEnumerable<double> MapSelector(DifficultyData mapData);

/// <summary> Various statistics methods for bot analysis </summary>
public static class BotStatistics
{
    /// <summary> Gets the angle change between consecutive swings </summary>
    public static IEnumerable<double> AngleChangeBetweenSwings(IEnumerable<SwingData> swings)
    {
        List<SwingData> list = [.. swings];
        for (int i = 1; i < list.Count; i++)
            yield return Math.Abs(list[i].StartFrame.dir.ToRotation(list[i]) - list[i - 1].EndFrame.dir.ToRotation(list[i - 1]));
    }
    /// <summary> Gets the reposition distance between consecutive swings </summary>
    public static IEnumerable<double> RepositionBetweenSwings(IEnumerable<SwingData> swings)
    {
        List<SwingData> list = [.. swings];
        for (int i = 1; i < list.Count; i++)
            yield return (new Vector2(list[i].StartFrame.x, list[i].StartFrame.y) - new Vector2(list[i - 1].EndFrame.x, list[i - 1].EndFrame.y)).Length();
    }
    /// <summary> Gets the time between consecutive swings in seconds </summary>
    public static IEnumerable<double> TimeBetweenSwings(IEnumerable<SwingData> swings)
    {
        List<SwingData> list = [.. swings];
        for (int i = 1; i < list.Count; i++)
            yield return (list[i].StartFrame.ms - list[i - 1].EndFrame.ms) / 1000;
    }
    /// <summary> Gets the size of each swing in a list of consecutive swings </summary>
    public static IEnumerable<double> SizeOfSwings(IEnumerable<SwingData> swings)
    {
        List<SwingData> list = [.. swings];
        for (int i = 0; i < list.Count; i++)
            yield return (new Vector2(list[i].EndFrame.x, list[i].EndFrame.y) - new Vector2(list[i].StartFrame.x, list[i].StartFrame.y)).Length() + 1;
    }
    /// <summary> Gets the speed of each swing in a list of consecutive swings </summary>
    public static IEnumerable<double> EBPM(IEnumerable<SwingData> swings)
        => swings.Select(s => (double)s.SwingEBPM);
}