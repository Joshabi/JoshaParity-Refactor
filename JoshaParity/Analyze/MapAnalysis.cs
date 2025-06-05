using JoshaParity.Processors;
using JoshaParser.Data.Beatmap;
using JoshaParser.Data.Metadata;
using JoshaParser.Parsers;
using JoshaParser.Utils;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace JoshaParity.Analyze;

/// <summary> Optimized lazy-loading analysis wrapper for Beatmaps </summary>
public class BeatmapAnalysis(Beatmap beatmap, MapAnalyserConfig? config = null)
{
    public Beatmap Beatmap { get; } = beatmap;
    public MapAnalyserConfig Config { get; private set; } = config ?? new();

    private readonly ConcurrentDictionary<(string characteristic, BeatmapDifficultyRank rank), Lazy<Task<DifficultyAnalysis?>>> _analyses = new();

    /// <summary> Get analysis for specific difficulty (lazy-loaded) </summary>
    public async Task<DifficultyAnalysis?> GetAnalysisAsync(string characteristic, BeatmapDifficultyRank rank)
    {
        if (string.IsNullOrWhiteSpace(characteristic)) return null;

        (string characteristic, BeatmapDifficultyRank rank) key = (characteristic, rank);
        Lazy<Task<DifficultyAnalysis?>> lazyAnalysis = _analyses.GetOrAdd(key, _ => new Lazy<Task<DifficultyAnalysis?>>(() => ComputeAnalysisAsync(key)));
        return await lazyAnalysis.Value;
    }

    /// <summary> Get analysis for difficulty info </summary>
    public Task<DifficultyAnalysis?> GetAnalysisAsync(DifficultyInfo difficulty) =>
        GetAnalysisAsync(difficulty.Characteristic, difficulty.Rank);

    /// <summary> Preloads a cached analysis for all difficulties </summary>
    public async Task PreloadAllAsync()
    {
        if (Beatmap.SongData.DifficultyBeatmaps == null)
            return;

        IEnumerable<Task<DifficultyAnalysis?>> tasks = Beatmap.SongData.DifficultyBeatmaps
            .Select(diff => GetAnalysisAsync(diff.Characteristic, diff.Rank));

        await Task.WhenAll(tasks);
    }

    /// <summary> Update config and clear cache </summary>
    public void UpdateConfig(MapAnalyserConfig newConfig)
    {
        Config = newConfig ?? new();
        _analyses.Clear();
    }

    /// <summary> Clear all cached analyses </summary>
    public void ClearCache() => _analyses.Clear();

    /// <summary> Gets a specific difficulty analysis by characteristic and rank </summary>
    private async Task<DifficultyAnalysis?> ComputeAnalysisAsync((string characteristic, BeatmapDifficultyRank rank) key)
    {
        try {
            DifficultyInfo? diffInfo = FindDifficultyInfo(key);
            if (diffInfo == null) {
                Trace.WriteLine($"Difficulty not found: {key}");
                return null;
            }

            DifficultyData? data = Beatmap.FetchDifficulty(diffInfo);
            if (data == null) {
                Trace.WriteLine($"Unable to load difficulty data for {key}");
                return null;
            }

            return await Task.Run(() =>
            {
                BPMContext bpmContext = CreateBpmContext(data);
                MapObjects objects = BeatmapAnalyser.CreateMapObjects(data, bpmContext);
                Data.BotState botState = MapProcessor.Run(objects, bpmContext, Config.AnalyserConfig);
                return new DifficultyAnalysis(data, bpmContext, botState);
            }).ConfigureAwait(false);
        } catch (Exception ex) {
            Trace.WriteLine($"Error analyzing {key}: {ex.Message}");
            return null;
        }
    }

    /// <summary> Helper - Creates a BPM context from the beatmap data </summary>
    private BPMContext CreateBpmContext(DifficultyData data) =>
        Beatmap.AudioData?.ToBPMContext(Beatmap.SongData.Song.BPM, Beatmap.SongData.SongTimeOffset) ??
        BPMContext.CreateBPMContext(Beatmap.SongData.Song.BPM, data.RawBPMEvents, Beatmap.SongData.SongTimeOffset);

    /// <summary> Helper - Finds difficulty info by characteristic and rank </summary>
    private DifficultyInfo? FindDifficultyInfo((string characteristic, BeatmapDifficultyRank rank) key) =>
        Beatmap.SongData.DifficultyBeatmaps
            .FirstOrDefault(d =>
                string.Equals(d.Characteristic, key.characteristic, StringComparison.OrdinalIgnoreCase) &&
                d.Rank == key.rank);
}

/// <summary> Analysis configuration </summary>
public class MapAnalyserConfig
{
    public bool PrecalculateAllDifficulties { get; set; } = false;
    public AnalysisConfig AnalyserConfig { get; set; } = new();
}

/// <summary> General Analysis Factory functionalities </summary>
public static class BeatmapAnalyser
{
    /// <summary> Creates a new BeatmapAnalysis instance from a Beatmap object </summary>
    public static BeatmapAnalysis CreateFromBeatmap(Beatmap beatmap, MapAnalyserConfig? config = null) {
        config ??= new MapAnalyserConfig();
        BeatmapAnalysis analysis = new(beatmap, config);
        if (config.PrecalculateAllDifficulties == true) {
            Task.Run(() => analysis.PreloadAllAsync()).GetAwaiter().GetResult();
        }
        return analysis;
    }

    /// <summary> Creates a new BeatmapAnalysis instance from  </summary>
    public static BeatmapAnalysis CreateFromJson(string info, AudioInfo? audioData = null, MapAnalyserConfig? config = null)
    {
        SongInfo songInfo = BeatmapLoader.LoadSongInfoFromString(info) ??
            throw new ArgumentException("Invalid song info JSON");
        Beatmap beatmap = new(songInfo, audioData);
        return new BeatmapAnalysis(beatmap, config);
    }

    /// <summary> Analyzes a single difficulty data and returns a DifficultyAnalysis </summary>
    public static DifficultyAnalysis AnalyzeSingle(DifficultyData data, float bpm, float offset, AudioInfo? audio = null, MapAnalyserConfig? config = null)
    {
        if (data is null) throw new ArgumentNullException(nameof(data), "Difficulty data cannot be null");
        config ??= new MapAnalyserConfig();

        BPMContext bpmContext = audio?.ToBPMContext(bpm, offset) ?? BPMContext.CreateBPMContext(bpm, data.RawBPMEvents, offset);
        MapObjects objects = BeatmapAnalyser.CreateMapObjects(data, bpmContext);
        Data.BotState state = MapProcessor.Run(objects, bpmContext, config.AnalyserConfig);
        return new DifficultyAnalysis(data, bpmContext, state);
    }

    /// <summary> Creates map objects from difficulty data and BPM context </summary>
    /// <remarks> The BPMContext MS calculations responsibility will be moved to JoshaParser eventually </remarks>
    public static MapObjects CreateMapObjects(DifficultyData data, BPMContext bpmContext)
    {
        List<Note> notes = data.Notes.Select(x => { x.MS = bpmContext.ToRealTime(x.B) * 1000; return x; }).ToList();
        List<Bomb> bombs = data.Bombs.Select(x => { x.MS = bpmContext.ToRealTime(x.B) * 1000; return x; }).ToList();
        List<Obstacle> obstacles = data.Obstacles.Select(x => { x.MS = bpmContext.ToRealTime(x.B) * 1000; return x; }).ToList();
        List<Arc> arcs = data.Arcs.Select(x => { x.MS = bpmContext.ToRealTime(x.B) * 1000; x.TMS = bpmContext.ToRealTime(x.TB) * 1000; return x; }).ToList();
        List<Chain> chains = data.Chains.Select(x => { x.MS = bpmContext.ToRealTime(x.B) * 1000; x.TMS = bpmContext.ToRealTime(x.TB) * 1000; return x; }).ToList();
        return new MapObjects(notes, bombs, obstacles, arcs, chains);
    }
}
