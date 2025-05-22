using System.Numerics;
using JoshaParity.Processors;
using JoshaParser.Data.Beatmap;

namespace JoshaParity.Data;

/// <summary> A snapshot of the bot's state information </summary>
public class BotState
{
    public BotState? Parent { get; }
    private readonly List<BotState> _children = [];
    public IReadOnlyList<BotState> Children => _children;

    public List<SwingData> LeftSwings { get; } = [];
    public List<SwingData> RightSwings { get; } = [];
    public BPMContext BPMContext { get; private set; }
    internal SwingBuffer SwingBuffer { get; private set; } = new();

    public Vector2 Position { get; set; } = new();
    public float BeatTime { get; set; } = 0;

    /// <summary> Creates a new BotState given previous BotState? and BPM context </summary>
    public BotState(BotState? parent, BPMContext bpmContext)
    {
        Parent = parent;
        BPMContext = bpmContext;
        parent?._children.Add(this);
    }

    /// <summary> Forks the mapstate setting itself as parent state </summary>
    public BotState Fork()
    {
        BotState clone = new(this, BPMContext)
        {
            BeatTime = this.BeatTime,
            Position = this.Position,
            SwingBuffer = this.SwingBuffer.Clone()
        };
        return clone;
    }

    /// <summary> Adds a swing for a given hand </summary>
    public void AddSwing(SwingData swing, Hand hand)
    {
        if (hand == Hand.Right) RightSwings.Add(swing);
        else LeftSwings.Add(swing);
    }

    /// <summary> Returns all swings completed so far on a given hand </summary>
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

    /// <summary> Returns all swings completed so far on both hands </summary>
    public List<SwingData> GetJointSwingData()
    {
        IEnumerable<SwingData> leftHandSwings = GetAllSwings(Hand.Left);
        IEnumerable<SwingData> rightHandSwings = GetAllSwings(Hand.Right);
        List<SwingData> combined = new(leftHandSwings);
        combined.AddRange(rightHandSwings);
        return [.. combined.OrderBy(x => x.StartFrame.beats)];
    }
}
