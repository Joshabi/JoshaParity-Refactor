using JoshaParity.Data;
using JoshaParity.Utils;
using JoshaParser.Data.Beatmap;
using System.Numerics;

namespace JoshaParity.Processors;

/// <summary> Container for map entities (Bombs, Walls, Notes) </summary>
public class MapObjects(List<Note> notes, List<Bomb> bombs, List<Obstacle> walls, List<Arc> arcs, List<Chain> chains)
{
    public List<Note> Notes { get; set; } = [.. notes];
    public List<Bomb> Bombs { get; } = [.. bombs];
    public List<Obstacle> Obstacles { get; } = [.. walls];
    public List<Arc> Arcs { get; } = [.. arcs];
    public List<Chain> Chains { get; } = [.. chains];
}

/// <summary> Settings for the analysis </summary>
/// <remarks> This will be greatly expanded in the future </remarks>
public class AnalysisSettings
{
    public float AngleTolerance { get; set; } = 270.0f;   // The limit on angle change to consider an angle reset
    public float AngleLimit { get; set; } = 180.0f;       // The limit on how far either way you can rotate
}

/// <summary> Simulates through mapdata and returns final bot state </summary>
public class MapProcessor
{
    /// <summary> Given map objects, simulates and returns the final bot state </summary>
    public static BotState Run(MapObjects mapObjects, BPMContext bpmContext, AnalysisSettings? config = null)
    {
        BotState initState = new(null, bpmContext, config);
        return SimulateMap(initState, mapObjects);
    }

    /// <summary> Simulates through the entire map data and returns the final bot state </summary>
    private static BotState SimulateMap(BotState startState, MapObjects data) => SimulateForward(startState, data, null, null);

    /// <summary> Simulate state forward by X of Y type objects </summary>
    public static BotState SimulateForwardByType<T>(MapObjects mapObjects, BPMContext bpmContext, int count, BotState? startState = null) where T : BeatGridObject => SimulateForward(startState ?? new BotState(null, bpmContext), mapObjects, count, o => o is T);

    /// <summary> Simulate state forward by X total objects </summary>
    public static BotState SimulateForwardByTotal(MapObjects mapObjects, BPMContext bpmContext, int count, BotState? startState = null) => SimulateForward(startState ?? new BotState(null, bpmContext), mapObjects, count, o => true);

    /// <summary> Simulates forward an amount of objects and stops when a predicate is met, returning the current bot state. </summary>
    private static BotState SimulateForward(
        BotState startState,
        MapObjects data,
        int? maxObjectCount = null,
        Func<BeatGridObject, bool>? predicate = null)
    {
        // Gather all map objects in one list
        List<BeatGridObject> mapObjects = data.Notes
            .Concat<BeatGridObject>(data.Chains)
            .Concat(data.Bombs)
            .Concat(data.Obstacles)
            .Concat(data.Arcs)
            .ToList();

        mapObjects.Sort((a, b) => a.B.CompareTo(b.B));
        mapObjects.RemoveAll(x => x.B < startState.BeatTime);
        BotState state = startState;

        // Sliding map context window of 2 beats
        float slidingWindowSize = 2.0f;
        int windowEndIndex = 0;
        Queue<BeatGridObject> contextWindow = new();

        // Iterate through all objects
        int processed = 0;
        for (int i = 0; i < mapObjects.Count; i++) {
            // Construct the context window
            BeatGridObject obj = mapObjects[i];
            float windowLimit = obj.B + slidingWindowSize;

            while (contextWindow.Count > 0 && contextWindow.Peek().B < obj.B)
                contextWindow.Dequeue();

            while (windowEndIndex < mapObjects.Count && mapObjects[windowEndIndex].B <= windowLimit) {
                if (mapObjects[windowEndIndex].B >= obj.B)
                    contextWindow.Enqueue(mapObjects[windowEndIndex]);
                windowEndIndex++;
            }

            // Simulation break condition
            if (predicate is null || predicate(obj)) {
                processed++;
                if (maxObjectCount.HasValue && processed > maxObjectCount.Value)
                    break;
            }

            // Swingable (Parity Objects)
            // TO DO: Method for handling Arcs and their influence
            if (obj is Note note and not Arc) {

                // Process note in the buffer. If we get a result we can build a swing with the notes
                Hand hand = note.C == 0 ? Hand.Left : Hand.Right;
                List<Note>? swingNotes = state.SwingBuffer.Process(note);
                if (swingNotes is not null && swingNotes.Count != 0) {
                    SwingData swing = GenerateSwing(state, [.. contextWindow], swingNotes, hand);
                    state.AddSwing(swing, hand);
                }

                if (hand == Hand.Left)
                    state.UpdatePose(note.B, leftHand: new Vector2(note.X, note.Y));
                else
                    state.UpdatePose(note.B, rightHand: new Vector2(note.X, note.Y));
            } else if (obj is Obstacle obstacle) {
                state.WallBuffer.Process(obstacle);
                state.WallBuffer.RemoveExpired(obstacle.B);
                state.UpdatePose(obstacle.B, obstacle: obstacle);
            }
        }

        // Flush the remaining note buffer
        foreach (Hand hand in Enum.GetValues(typeof(Hand))) {
            List<Note>? swingNotes = state.SwingBuffer.ForceFlush(hand);
            if (swingNotes is not null && swingNotes.Count != 0) {
                SwingData swing = GenerateSwing(state, [.. contextWindow], swingNotes, hand);
                state.AddSwing(swing, hand);
            }
        }

        return state;
    }

    /// <summary> Generates a swing given a list of notes </summary>
    private static SwingData GenerateSwing(BotState state, List<BeatGridObject> slidingContext, List<Note> swingNotes, Hand hand)
    {
        List<SwingData> leftSwingData = state.GetAllSwings(Hand.Left).ToList();
        List<SwingData> rightSwingData = state.GetAllSwings(Hand.Right).ToList();

        bool firstSwing = (hand == Hand.Right && rightSwingData.Count == 0) || (hand == Hand.Left && leftSwingData.Count == 0);
        SwingData? lastSwing = firstSwing ? null : (hand == Hand.Right ? rightSwingData.Last() : leftSwingData.Last());
        Parity initialParity = firstSwing
            ? ParityUtils.InitialParity[(int)swingNotes[0].D]
            : (lastSwing!.Parity == Parity.Backhand ? Parity.Forehand : Parity.Backhand);

        // Create the builder and set initial properties
        SwingDataBuilder builder = new SwingDataBuilder().WithHand(hand).WithNotes(swingNotes).WithParity(initialParity);
        builder.WithSwingType(SwingClassifier.Classify(swingNotes));
        builder.PathSwing(lastSwing);
        if (lastSwing is null)
            return builder.Build();

        (ResetType resetType, Parity predictedParity) = ParityUtils.AssessParity(state, builder.Build(), slidingContext);
        float swingEBPM = TimeUtils.SwingEBPM(state.BPMContext, lastSwing.EndFrame.beats, builder.Build().StartFrame.beats) * (lastSwing.IsReset ? 2 : 1);
        builder.WithEBPM(swingEBPM).WithResetType(resetType).WithParity(predictedParity);
        builder.PathSwing(lastSwing);

        // Check if any reversal is needed for pure dot swings (We can try every option once multi-pathing is implemented)
        builder.CheckDotReversal(lastSwing);

        // Add an empty swing prior to this swing if its a reset swing
        if (builder.IsReset)
            state.AddSwing(SwingUtils.GetInbetweenSwingData(lastSwing, builder.Build()), hand);

        return builder.Build();
    }
}
