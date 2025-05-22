using JoshaParity.Data;
using JoshaParity.Utils;
using JoshaParser.Data.Beatmap;

namespace JoshaParity.Processors;

/// <summary> Container for map entities (Bombs, Walls, Notes) </summary>
public class MapObjects(List<Note> notes, List<Bomb> bombs, List<Obstacle> walls, List<Arc> arcs, List<Chain> chains)
{
    public List<Note> Notes { get; set; } = new List<Note>(notes);
    public List<Bomb> Bombs { get; } = new List<Bomb>(bombs);
    public List<Obstacle> Obstacles { get; } = new List<Obstacle>(walls);
    public List<Arc> Arcs { get; } = new List<Arc>(arcs);
    public List<Chain> Chains { get; } = new List<Chain>(chains);
}

/// <summary> Simulates through mapdata and returns final bot state </summary>
public class MapProcessor
{
    /// <summary> Given map objects, simulates and returns the final bot state </summary>
    public static BotState Run(MapObjects mapObjects, BPMContext bpmContext)
    {
        BotState initState = new(null, bpmContext);
        return SimulateMap(initState, mapObjects);
    }

    /// <summary> Simulates map objects starting from a given state. Returns the final state after processing all objects. </summary>
    private static BotState SimulateMap(BotState startState, MapObjects data)
    {
        // Gather all map objects in one list
        List<BeatObject> mapObjects = data.Notes
            .Concat<BeatObject>(data.Chains)
            .Concat(data.Bombs)
            .Concat(data.Obstacles)
            .Concat(data.Arcs)
            .ToList();

        mapObjects.Sort((a, b) => a.B.CompareTo(b.B));
        mapObjects.RemoveAll(x => x.B < startState.BeatTime);
        BotState currentState = startState;

        // Sliding map context window of 2 beats
        float slidingWindowSize = 2.0f;
        int windowEndIndex = 0;
        Queue<BeatObject> contextWindow = new();

        // Iterate through all objects
        for (int i = 0; i < mapObjects.Count; i++)
        {
            // Construct the context window
            BeatObject obj = mapObjects[i];
            float windowLimit = obj.B + slidingWindowSize;

            while (contextWindow.Count > 0 && contextWindow.Peek().B < obj.B)
                contextWindow.Dequeue();

            while (windowEndIndex < mapObjects.Count && mapObjects[windowEndIndex].B <= windowLimit)
            {
                if (mapObjects[windowEndIndex].B >= obj.B)
                    contextWindow.Enqueue(mapObjects[windowEndIndex]);
                windowEndIndex++;
            }

            // Temporary: If not a note, we don't care
            if (obj is not Note note) { continue; }

            // Process note in the buffer. If we get a result we can build a swing with the notes
            List<Note>? swingNotes = currentState.SwingBuffer.Process(note);
            if (swingNotes is not null && swingNotes.Count != 0) {
                Hand hand = note.C == 0 ? Hand.Left : Hand.Right;
                SwingData swing = GenerateSwing(currentState, swingNotes, hand);
                currentState.AddSwing(swing, hand);
            }
        }

        return currentState;
    }

    /// <summary> Generates a swing given a list of notes </summary>
    private static SwingData GenerateSwing(BotState state, List<Note> swingNotes, Hand hand)
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

        (ResetType resetType, Parity predictedParity) = ParityUtils.AssessParity(state, builder.Build());
        float swingEBPM = TimeUtils.SwingEBPM(state.BPMContext, lastSwing.EndFrame.beats, builder.Build().StartFrame.beats) * (lastSwing.IsReset ? 2 : 1);
        builder.WithEBPM(swingEBPM).WithResetType(resetType).WithParity(predictedParity);
        builder.PathSwing(lastSwing);

        // ADD THE DOT CHECKS YOU MUPPET

        return builder.Build();
    }
}
