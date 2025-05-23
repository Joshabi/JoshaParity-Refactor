using System.Numerics;
using JoshaParity.Processors;
using JoshaParity.Utils;
using JoshaParser.Data.Beatmap;

namespace JoshaParity.Data;

/// <summary> A snapshot of the positional data for the bot </summary>
public readonly struct BotPose(float beatTime, Vector2 leftHandPosition, Vector2 rightHandPosition, Vector2 headPosition)
{
    public float BeatTime { get; } = beatTime;
    public Vector2 LeftHandPosition { get; } = leftHandPosition;
    public Vector2 RightHandPosition { get; } = rightHandPosition;
    public Vector2 HeadPosition { get; } = headPosition;

    public bool IsSameAs(BotPose other)
    {
        return BeatTime == other.BeatTime
            && LeftHandPosition == other.LeftHandPosition
            && RightHandPosition == other.RightHandPosition
            && HeadPosition == other.HeadPosition;
    }
}

/// <summary> A snapshot of the bot's state information </summary>
public class BotState
{
    // State tree information
    public BotState? Parent { get; }
    private readonly List<BotState> _children = [];
    public IReadOnlyList<BotState> Children => _children;

    // Recorded data and contextual components
    public List<SwingData> LeftSwings { get; } = [];
    public List<SwingData> RightSwings { get; } = [];
    public List<BotPose> MovementHistory { get; } = [];
    public BPMContext BPMContext { get; private set; }
    internal SwingBuffer SwingBuffer { get; private set; } = new();

    // Contextual data
    public float BeatTime { get; set; } = 0;
    public Vector2 LeftHandPosition { get; set; } = new();
    public Vector2 RightHandPosition { get; set; } = new();
    public Vector2 HeadPosition { get; set; } = new();

    /// <summary> Creates a new BotState given previous BotState? and BPM context </summary>
    public BotState(BotState? parent, BPMContext bpmContext)
    {
        Parent = parent;
        BPMContext = bpmContext;
        parent?._children.Add(this);

        if (parent is null) return;

        LeftHandPosition = parent.LeftHandPosition;
        RightHandPosition = parent.RightHandPosition;
        HeadPosition = parent.HeadPosition;
    }

    /// <summary> Forks the mapstate setting itself as parent state </summary>
    public BotState Fork()
    {
        BotState clone = new(this, BPMContext)
        {
            BeatTime = this.BeatTime,
            SwingBuffer = this.SwingBuffer.Clone(),
            LeftHandPosition = this.LeftHandPosition,
            RightHandPosition = this.RightHandPosition,
            HeadPosition = this.HeadPosition
        };
        return clone;
    }

    /// <summary> Adds a swing for a given hand </summary>
    public void AddSwing(SwingData swing, Hand hand)
    {
        if (hand == Hand.Right) RightSwings.Add(swing);
        else LeftSwings.Add(swing);
    }

    /// <summary> Updates the bots position and creates a new movement record if appropriate </summary>
    public void UpdatePose(float beatTime, Vector2? leftHand = null, Vector2? rightHand = null)
    {
        Vector2 newLeft = leftHand ?? LeftHandPosition;
        Vector2 newRight = rightHand ?? RightHandPosition;
        Vector2 newHead = (newLeft + newRight) / 2;
        newHead.Y = 1.5f;                                      // Needed for now as currently no duck detection
        newHead.X = SwingUtils.Clamp(newHead.X += 0.5f, 1,3);  // Temporary
        BotPose newPose = new(beatTime, newLeft, newRight, newHead);

        if (MovementHistory.Count == 0 || !MovementHistory[MovementHistory.Count-1].IsSameAs(newPose)) {
            MovementHistory.Add(newPose);
            LeftHandPosition = newLeft;
            RightHandPosition = newRight;
            HeadPosition = newHead;
            BeatTime = beatTime;
        }
    }

    /// <summary> Returns all swings records from this state and its ancestors. </summary>
    public IEnumerable<SwingData> GetAllSwings(Hand hand)
    {
        Stack<BotState> stack = new();
        for (var s = this; s != null; s = s.Parent)
            stack.Push(s);

        foreach (var state in stack)
        {
            var swings = hand == Hand.Right ? state.RightSwings : state.LeftSwings;
            foreach (var swing in swings) yield return swing;
        }
    }

    /// <summary> Returns all movement history records from this state and its ancestors. </summary>
    public List<BotPose> GetAllMovementHistory()
    {
        Stack<BotState> stack = new();
        for (var s = this; s != null; s = s.Parent)
            stack.Push(s);

        List<BotPose> allPoses = [];
        foreach (var state in stack)
            allPoses.AddRange(state.MovementHistory);

        return [.. allPoses.OrderBy(p => p.BeatTime)];
    }

    /// <summary> Returns all swings completed so far on both hands </summary>
    public List<SwingData> GetJointSwingData()
    {
        IEnumerable<SwingData> leftHandSwings = GetAllSwings(Hand.Left);
        IEnumerable<SwingData> rightHandSwings = GetAllSwings(Hand.Right);
        List<SwingData> combined = new(leftHandSwings);
        combined.AddRange(rightHandSwings);
        return [.. combined.OrderBy(x => x.StartFrame.beats)];
    }

    /// <summary> Debug useful information about the bots state </summary>
    public override string ToString()
    {
        return
            $"BotState:\n" +
            $"  BeatTime: {BeatTime}\n" +
            $"  LeftHandPosition: {LeftHandPosition}\n" +
            $"  RightHandPosition: {RightHandPosition}\n" +
            $"  HeadPosition: {HeadPosition}\n" +
            $"  MovementHistory: {MovementHistory.Count} poses\n" +
            $"  LeftSwings: {LeftSwings.Count}\n" +
            $"  RightSwings: {RightSwings.Count}\n" +
            $"  Parent: {(Parent != null ? "Yes" : "No")}\n" +
            $"  Children: {Children.Count}";
    }
}
