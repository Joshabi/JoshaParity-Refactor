using JoshaParity.Analyze.Statistics;
using JoshaParity.Data;
using JoshaParser.Data.Beatmap;
using JoshaParser.Data.Metadata;
using System.Text;

namespace JoshaParity.Analyze;

/// <summary> Indication of which hand a result is for </summary>
public enum HandResult { Left, Right, Both }

/// <summary> Analyse map and bot data with caching </summary>
public class DifficultyAnalysis(DifficultyData diffData, BPMContext bpmContext, BotState state)
{
    private readonly DifficultyData _diffData = diffData;
    private readonly BotState _state = state;
    private readonly BPMContext _bpmContext = bpmContext;

    private readonly Dictionary<StatCacheKey, double> _statCache = [];

    /// <summary> Gets the associated BPM Context for this difficulty </summary>
    public BPMContext GetBPMContext() => _bpmContext;

    /// <summary> Returns the finalized bot state </summary>
    public BotState GetBotState() => _state;

    /// <summary> Returns swing data for a given hand </summary>
    public IEnumerable<SwingData> GetSwingData(HandResult hand)
    {
        return hand switch
        {
            HandResult.Left => _state.GetAllSwings(Hand.Left),
            HandResult.Right => _state.GetAllSwings(Hand.Right),
            HandResult.Both => _state.GetJointSwingData(),
            _ => []
        };
    }

    /// <summary> Returns movement history for the bot </summary>
    public List<BotPose> GetMovementData() => _state.GetAllMovementHistory();

    /// <summary> General-purpose statistic API for swing-based statistics. </summary>
    public double GetSwingStatistic(HandResult hand, IStatisticMethod method, SwingSelector selector)
    {
        StatCacheKey key = new(method.Identifier, hand, selector.Method.Name);
        if (_statCache.TryGetValue(key, out double result)) return result;
        IEnumerable<SwingData> swings = GetSwingData(hand);
        IEnumerable<double> data = selector(swings);
        result = method.Calculate(data);
        _statCache[key] = result;
        return result;
    }

    /// <summary> Gets a statistic for angle change between swings </summary>
    public double GetAngleChangeStat(HandResult hand, IStatisticMethod method) => GetSwingStatistic(hand, method, BotStatistics.AngleChangeBetweenSwings);

    /// <summary> Gets a statistic for repositioning between swings </summary>
    public double GetRepositionStat(HandResult hand, IStatisticMethod method) => GetSwingStatistic(hand, method, BotStatistics.RepositionBetweenSwings);

    /// <summary> Gets a statistic for time between swings </summary>
    public double GetTimeBetweenSwingsStat(HandResult hand, IStatisticMethod method) => GetSwingStatistic(hand, method, BotStatistics.TimeBetweenSwings);

    /// <summary> Gets a statistic for size of swings </summary>
    public double GetSizeOfSwingsStat(HandResult hand, IStatisticMethod method) => GetSwingStatistic(hand, method, BotStatistics.SizeOfSwings);

    /// <summary> Gets a statistic for effective BPM of swings </summary>
    public double GetEBPMStat(HandResult hand, IStatisticMethod method) => GetSwingStatistic(hand, method, BotStatistics.EBPM);

    /// <summary> Gets the total reset counts </summary>
    public int GetResetCount(ResetType type = ResetType.Angle, HandResult hand = HandResult.Both)
    {
        IEnumerable<SwingData> swings = GetSwingData(hand);
        return swings == null || swings.Count() <= 1 ? 0 : swings.Count(x => x.ResetType == type);
    }

    /// <summary> Gets the average NPS across the difficulty </summary>
    public double GetNPS(HandResult hand = HandResult.Both)
    {
        StatCacheKey key = new("Mean", hand, nameof(GetNPS));
        if (_statCache.TryGetValue(key, out double cached)) return cached;

        IEnumerable<Note> notes = hand == HandResult.Both ? _diffData.Notes : _diffData.Notes.Where(n => n.C == (hand == HandResult.Left ? 0 : 1));
        if (!notes.Any()) return 0;

        float mappedDurationSeconds = notes.Last().MS / notes.First().MS / 1000f;
        float result = mappedDurationSeconds > 0 ? notes.Count() / mappedDurationSeconds : 0;

        _statCache[key] = result;
        return result;
    }

    /// <summary> Gets the average SPS across the difficulty </summary>
    public double GetSPS(HandResult hand = HandResult.Both)
    {
        StatCacheKey key = new("Mean", hand, nameof(GetSPS));
        if (_statCache.TryGetValue(key, out double cached)) return cached;

        if (hand == HandResult.Both) {
            double result = GetSPS(HandResult.Left) + GetSPS(HandResult.Right);
            _statCache[key] = result;
            return result;
        }

        IEnumerable<SwingData> swings = GetSwingData(hand);
        if (swings == null || swings.Count() <= 1) return 0;
        float duration = swings.Last().EndFrame.ms - swings.First().StartFrame.ms;
        return duration > 0 ? swings.Count() / duration : 0;
    }

    /// <summary> Gets the percentage of swings on a hand </summary>
    public double GetHandedness(HandResult hand = HandResult.Right)
    {
        StatCacheKey key = new("Percentage", hand, nameof(GetHandedness));
        if (_statCache.TryGetValue(key, out double cached)) return cached;

        IEnumerable<SwingData> leftSwings = GetSwingData(HandResult.Left);
        IEnumerable<SwingData> rightSwings = GetSwingData(HandResult.Right);
        int swingTotal = leftSwings.Count() + rightSwings.Count();
        if (swingTotal <= 0) return 0;

        double result = 100;
        if (hand == HandResult.Left) {
            result = leftSwings.Count() == 0 ? 0 : (float)leftSwings.Count() / swingTotal * 100;
        } else if (hand == HandResult.Right) {
            result = rightSwings.Count() == 0 ? 0 : (float)rightSwings.Count() / swingTotal * 100;
        }

        _statCache[key] = result;
        return result;
    }

    /// <summary> Gets the percentage of a type of swing </summary>
    public double GetSwingTypePercent(SwingType type = SwingType.Normal, HandResult hand = HandResult.Both)
    {
        StatCacheKey key = new("Percentage", hand, nameof(GetSwingTypePercent) + type.ToString());
        if (_statCache.TryGetValue(key, out double cachedValue)) return cachedValue;

        double result = 0;
        switch (hand) {
            case HandResult.Left:
            IEnumerable<SwingData> leftSwings = GetSwingData(HandResult.Left);
            result = leftSwings.Count() == 0 ? 0 : (double)leftSwings.Count(x => x.SwingType == type) / leftSwings.Count() * 100;
            break;
            case HandResult.Right:
            IEnumerable<SwingData> rightSwings = GetSwingData(HandResult.Left);
            result = rightSwings.Count() == 0 ? 0 : (double)rightSwings.Count(x => x.SwingType == type) / rightSwings.Count() * 100;
            break;
            case HandResult.Both:
            IEnumerable<SwingData> swings = GetSwingData(HandResult.Both);
            result = swings.Count() == 0 ? 0 : (double)swings.Count(x => x.SwingType == type) / swings.Count() * 100;
            break;
        }

        _statCache[key] = result;
        return result;
    }

    /// <summary> Gets the percentage of doubles in a difficulty </summary>
    /// TO DO: Rework behaviour so that its the % of swings on a hand that are in doubles, with HandResult.Both returning % of all swings being in doubles
    public double GetDoublesPercent()
    {
        StatCacheKey key = new("Percentage", HandResult.Both, nameof(GetDoublesPercent));
        if (_statCache.TryGetValue(key, out double cachedValue)) return cachedValue;

        IEnumerable<SwingData> leftHand = GetSwingData(HandResult.Left).Where(x => x.Notes.Count > 0);
        IEnumerable<SwingData> rightHand = GetSwingData(HandResult.Right).Where(x => x.Notes.Count > 0);

        const double threshold = 0.05;
        int matchedSwings = leftHand.Count(leftSwing =>
            rightHand.Any(rightSwing =>
                Math.Abs(leftSwing.Notes[0].MS - rightSwing.Notes[0].MS) <= threshold));

        int totalSwings = leftHand.Count() + rightHand.Count();
        float result = totalSwings == 0 ? 0 : (float)matchedSwings / totalSwings * 100;

        _statCache[key] = result;
        return result;
    }

    /// <summary> Clears all cached stat results </summary>
    public void ClearCache() => _statCache.Clear();

    public override string ToString()
    {
        StringBuilder sb = new();
        sb.AppendLine("-----------------------");
        sb.AppendLine("Map Data:");
        sb.AppendLine(_diffData.ToString());
        sb.AppendLine("-----------------------");
        sb.AppendLine($"Total Swings:");
        sb.AppendLine($" - Left Hand: {GetSwingData(HandResult.Left).Count()}");
        sb.AppendLine($" - Right Hand: {GetSwingData(HandResult.Right).Count()}");
        sb.AppendLine("Potential Resets:");
        sb.AppendLine($" - Normal Resets: {GetResetCount(ResetType.Angle)}");
        sb.AppendLine($" - Bomb Resets: {GetResetCount(ResetType.Bomb)}");
        sb.AppendLine("Average Swings per Second (SPS):");
        sb.AppendLine($" - Total: {GetSPS():F2}");
        sb.AppendLine($" - Left Hand: {GetSPS(HandResult.Left):F2}");
        sb.AppendLine($" - Right Hand: {GetSPS(HandResult.Right):F2}");
        sb.AppendLine($"Average Effective BPM:");
        sb.AppendLine($" - Total: {GetEBPMStat(HandResult.Both, new MeanStatistic()):F2}");
        sb.AppendLine($" - Left Hand: {GetEBPMStat(HandResult.Left, new MeanStatistic()):F2}");
        sb.AppendLine($" - Right Hand: {GetEBPMStat(HandResult.Right, new MeanStatistic()):F2}");
        sb.AppendLine("Handedness %:");
        sb.AppendLine($" - Right Hand: {GetHandedness(HandResult.Right):F2}%");
        sb.AppendLine($" - Left Hand: {GetHandedness(HandResult.Left):F2}%");
        sb.AppendLine("Percentage of Swing Types:");
        foreach (SwingType st in Enum.GetValues(typeof(SwingType))) {
            sb.AppendLine($" - {st}: {GetSwingTypePercent(st):F2}%");
        }
        sb.AppendLine("-----------------------");
        return sb.ToString();
    }
}