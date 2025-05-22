using JoshaParity.Data;
using System.Numerics;
using System.Text;
using JoshaParity.Utils;
using JoshaParser.Data.Beatmap;
using JoshaParser.Data.Metadata;

namespace JoshaParity.Analyze;

/// <summary> Indication of which hand a result is for </summary>
public enum HandResult
{
    Left, Right, Both
}

/// <summary> Analyse map and bot data with caching </summary>
public class DifficultyAnalysis(DifficultyData diffData, BPMContext bpmContext, BotState state)
{
    private readonly DifficultyData _diffData = diffData;
    private readonly BotState _state = state;
    private readonly BPMContext _bpmContext = bpmContext;

    // Cache storage for stat computations keyed by stat name + hand result
    private readonly Dictionary<(string StatName, HandResult Hand), float> _cache = [];

    private List<SwingData> GetSwingData(HandResult hand) => hand switch
    {
        HandResult.Left => _state.GetAllSwings(Hand.Left).ToList(),
        HandResult.Right => _state.GetAllSwings(Hand.Right).ToList(),
        HandResult.Both => _state.GetJointSwingData(),
        _ => []
    };

    public float GetNPS(HandResult hand)
    {
        var key = (nameof(GetNPS), hand);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        int handColour = hand == HandResult.Left ? 0 : 1;
        IEnumerable<Note> notes = hand == HandResult.Both ? _diffData.Notes : _diffData.Notes.Where(n => n.C == handColour);

        if (!notes.Any()) return 0;

        var firstMs = notes.First().MS;
        var lastMs = notes.Last().MS;
        var durationSeconds = (lastMs - firstMs) / 1000f;

        var result = durationSeconds > 0 ? notes.Count() / durationSeconds : 0;

        _cache[key] = result;
        return result;
    }

    public int GetResetCount(ResetType type = ResetType.Angle)
    {
        var jointSwings = GetSwingData(HandResult.Both);
        if (jointSwings == null || jointSwings.Count <= 1) return 0;
        return jointSwings.Count(x => x.ResetType == type);
    }

    public float GetSPS(HandResult hand = HandResult.Both)
    {
        var key = (nameof(GetSPS), hand);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        float CalculateSPS(List<SwingData> swings)
        {
            if (swings == null || swings.Count <= 1) return 0;
            var firstBeat = swings.First().StartFrame.beats;
            var lastBeat = swings.Last().EndFrame.beats;
            var duration = TimeUtils.BeatsToSeconds(_bpmContext, firstBeat, lastBeat);
            return duration > 0 ? swings.Count / duration : 0;
        }

        var leftSPS = CalculateSPS(GetSwingData(HandResult.Left));
        var rightSPS = CalculateSPS(GetSwingData(HandResult.Right));

        float result = hand switch
        {
            HandResult.Left => leftSPS,
            HandResult.Right => rightSPS,
            HandResult.Both => leftSPS + rightSPS,
            _ => 0
        };

        _cache[key] = result;
        return result;
    }

    public float GetAverageEBPM(HandResult hand = HandResult.Both)
    {
        var key = (nameof(GetAverageEBPM), hand);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var leftHand = GetSwingData(HandResult.Left);
        var rightHand = GetSwingData(HandResult.Right);

        float result = hand switch
        {
            HandResult.Left => leftHand.Count == 0 ? 0 : leftHand.Average(x => x.SwingEBPM),
            HandResult.Right => rightHand.Count == 0 ? 0 : rightHand.Average(x => x.SwingEBPM),
            HandResult.Both => (leftHand.Count + rightHand.Count) == 0 ? 0 : GetSwingData(HandResult.Both).Average(x => x.SwingEBPM),
            _ => 0
        };

        _cache[key] = result;
        return result;
    }

    public Vector2 GetHandedness()
    {
        return new Vector2(GetHandedness(HandResult.Right), GetHandedness(HandResult.Left));
    }

    public float GetHandedness(HandResult hand = HandResult.Right)
    {
        var key = (nameof(GetHandedness), hand);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var leftHand = GetSwingData(HandResult.Left);
        var rightHand = GetSwingData(HandResult.Right);
        int swingCount = leftHand.Count + rightHand.Count;
        if (swingCount == 0) return 0;

        float result = hand switch
        {
            HandResult.Left => leftHand.Count == 0 ? 0 : (float)leftHand.Count / swingCount * 100,
            HandResult.Right or HandResult.Both => rightHand.Count == 0 ? 0 : (float)rightHand.Count / swingCount * 100,
            _ => 0
        };

        _cache[key] = result;
        return result;
    }

    public float GetSwingTypePercent(SwingType type = SwingType.Normal, HandResult hand = HandResult.Both)
    {
        var key = (nameof(GetSwingTypePercent) + type.ToString(), hand);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var leftHand = GetSwingData(HandResult.Left);
        var rightHand = GetSwingData(HandResult.Right);

        float result = hand switch
        {
            HandResult.Left => leftHand.Count == 0 ? 0 : leftHand.Count(x => x.SwingType == type) / (float)leftHand.Count * 100,
            HandResult.Right => rightHand.Count == 0 ? 0 : rightHand.Count(x => x.SwingType == type) / (float)rightHand.Count * 100,
            HandResult.Both => (leftHand.Count + rightHand.Count) == 0 ? 0 : GetSwingData(HandResult.Both).Count(x => x.SwingType == type) / (float)(leftHand.Count + rightHand.Count) * 100,
            _ => 0
        };

        _cache[key] = result;
        return result;
    }

    public float GetDoublesPercent()
    {
        var key = (nameof(GetDoublesPercent), HandResult.Both);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var leftHand = GetSwingData(HandResult.Left).Where(x => x.Notes.Count > 0);
        var rightHand = GetSwingData(HandResult.Right).Where(x => x.Notes.Count > 0);

        const double threshold = 0.05;
        int matchedSwings = leftHand.Count(leftSwing =>
            rightHand.Any(rightSwing =>
                Math.Abs(leftSwing.Notes[0].MS - rightSwing.Notes[0].MS) <= threshold));

        int totalSwings = leftHand.Count() + rightHand.Count();

        float result = totalSwings == 0 ? 0 : (float)matchedSwings / totalSwings * 100;

        _cache[key] = result;
        return result;
    }

    public float GetAverageSpacing(HandResult hand = HandResult.Right)
    {
        var key = (nameof(GetAverageSpacing), hand);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var handSwings = hand == HandResult.Left ? GetSwingData(HandResult.Left) : GetSwingData(HandResult.Right);
        if (handSwings == null || handSwings.Count <= 1) return 0;

        float result = handSwings.Zip(handSwings.Skip(1), (current, next) =>
        {
            float dX = next.StartFrame.x - current.EndFrame.x;
            float dY = next.StartFrame.y - current.EndFrame.y;
            return (float)Math.Sqrt(dX * dX + dY * dY);
        }).Average();

        _cache[key] = result;
        return result;
    }

    public float GetAverageAngleChange(HandResult hand = HandResult.Right)
    {
        var key = (nameof(GetAverageAngleChange), hand);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        float CalculateAverageAngle(IEnumerable<SwingData> swings)
        {
            var list = swings.ToList();
            if (list.Count <= 1) return 0;

            return list.Zip(list.Skip(1), (current, next) =>
                Math.Abs(next.StartFrame.dir.ToRotation(next) - current.EndFrame.dir.ToRotation(current))).Average();
        }

        var leftAngle = CalculateAverageAngle(GetSwingData(HandResult.Left));
        var rightAngle = CalculateAverageAngle(GetSwingData(HandResult.Right));

        float result = hand switch
        {
            HandResult.Left => leftAngle,
            HandResult.Right => rightAngle,
            HandResult.Both => (leftAngle + rightAngle) / 2,
            _ => 0
        };

        _cache[key] = result;
        return result;
    }

    /// <summary> Clears all cached stat results </summary>
    public void ClearCache() => _cache.Clear();
     
    public override string ToString()
    {
        StringBuilder sb = new();
        sb.AppendLine("-----------------------");
        sb.AppendLine("Map Data:");
        sb.AppendLine(_diffData.ToString());
        sb.AppendLine("-----------------------");
        sb.AppendLine($"Total Swings:");
        sb.AppendLine($" - Left Hand: {GetSwingData(HandResult.Left).Count}");
        sb.AppendLine($" - Right Hand: {GetSwingData(HandResult.Right).Count}");
        sb.AppendLine("Potential Resets:");
        sb.AppendLine($" - Normal Resets: {GetResetCount(ResetType.Angle)}");
        sb.AppendLine($" - Bomb Resets: {GetResetCount(ResetType.Bomb)}");
        sb.AppendLine("Average Swings per Second (SPS):");
        sb.AppendLine($" - Total: {GetSPS():F2}");
        sb.AppendLine($" - Left Hand: {GetSPS(HandResult.Left):F2}");
        sb.AppendLine($" - Right Hand: {GetSPS(HandResult.Right):F2}");
        sb.AppendLine($"Average Effective BPM:");
        sb.AppendLine($" - Total: {GetAverageEBPM():F2}");
        sb.AppendLine($" - Left Hand: {GetAverageEBPM(HandResult.Left):F2}");
        sb.AppendLine($" - Right Hand: {GetAverageEBPM(HandResult.Right):F2}");
        sb.AppendLine("Handedness %:");
        sb.AppendLine($" - Right Hand: {GetHandedness(HandResult.Right):F2}%");
        sb.AppendLine($" - Left Hand: {GetHandedness(HandResult.Left):F2}%");
        sb.AppendLine("Percentage of Swing Types:");
        foreach (SwingType st in Enum.GetValues(typeof(SwingType)))
        {
            sb.AppendLine($" - {st}: {GetSwingTypePercent(st):F2}%");
        }
        sb.AppendLine("-----------------------");
        return sb.ToString();
    }
}