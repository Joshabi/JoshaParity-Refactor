using System.Numerics;
using JoshaParity.Processors;
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
    internal WallBuffer WallBuffer { get; private set; } = new();

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
            WallBuffer = this.WallBuffer.Clone(),
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
    public void UpdatePose(float beatTime, Vector2? leftHand = null, Vector2? rightHand = null, Obstacle? obstacle = null)
    {
        Vector2 newLeft = leftHand ?? LeftHandPosition;
        Vector2 newRight = rightHand ?? RightHandPosition;

        var availableSpaces = WallBuffer.GetAvailableGridSpaces(beatTime);
        int currentX = (int)HeadPosition.X;
        int currentY = (int)HeadPosition.Y;
        int newX = currentX;
        int desiredY = currentY;

        // Attempt to return to neutral position if no influence in 2 beats
        if (beatTime - WallBuffer.LastDodgeInfluence >= 2 && currentX != 1)
            if (availableSpaces.Contains((1, currentY)))
                newX = 1;
        if (beatTime - WallBuffer.LastDuckInfluence >= 2 && currentY != 1)
            if (availableSpaces.Contains((newX, 1)))
                desiredY = 1;

        // If current position is blocked, follow priority of movements
        if (!availableSpaces.Contains((newX, desiredY)))
        {
            // Define candidate positions in priority order
            var candidatePositions = new List<(int x, int y)>
            {
                (1, 1), (2, 1), // 1. Center lanes without ducking
                (1, 0), (2, 0), // 2. Center lanes with ducking
                (0, 1), (3, 1), // 3. Outer lanes without ducking
                (0, 0), (3, 0), // 4. Outer lanes with ducking
            };

            // Find the first available position based on priority
            var availablePosition = candidatePositions.FirstOrDefault(pos => availableSpaces.Contains(pos));
            if (availablePosition != default) {
                newX = availablePosition.x;
                desiredY = availablePosition.y;
            } else if (availableSpaces.Count > 0) {
                // Fallback: pick the closest available space
                var (x, y) = availableSpaces.OrderBy(pos => Math.Abs(pos.x - newX) + Math.Abs(pos.y - desiredY)).First();
                newX = x; 
                desiredY = y;
            }
        }

        // Update last influence times if we had to move or duck
        if (newX != currentX)
            WallBuffer.LastDodgeInfluence = (obstacle is not null) ? obstacle.B + obstacle.D : beatTime;
        if (desiredY != currentY)
            WallBuffer.LastDuckInfluence = (obstacle is not null) ? obstacle.B + obstacle.D : beatTime;

        Vector2 newHead = new(newX, desiredY);
        BotPose newPose = new(beatTime, newLeft, newRight, newHead);
        if (MovementHistory.Count == 0 || !MovementHistory[MovementHistory.Count - 1].IsSameAs(newPose))
        {
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