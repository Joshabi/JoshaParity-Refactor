using JoshaParity.Data;
using JoshaParity.Processors;
using JoshaParser.Data.Beatmap;
using JoshaParser.Data.Metadata;
using JoshaParser.Parsers;
using JoshaParser.Utils;

namespace JoshaParity.Analyze;

/// <summary> An analysis object for a mapset </summary>
public class MapAnalysis(SongInfo mapData)
{
    /// <summary> Mapping of characteristic to Mapping of Rank and Analysis Object </summary>
    private readonly Dictionary<string, Dictionary<BeatmapDifficultyRank, DifficultyAnalysis>> _results =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IReadOnlyDictionary<BeatmapDifficultyRank, DifficultyAnalysis>> Results =>
        _results.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyDictionary<BeatmapDifficultyRank, DifficultyAnalysis>)kvp.Value);

    public SongInfo MapData { get; } = mapData;

    /// <summary> Adds an analysis result for a specific characteristic and difficulty rank </summary>
    public bool AddAnalysis(string characteristic, BeatmapDifficultyRank difficultyRank, DifficultyAnalysis analysis)
    {
        if (string.IsNullOrWhiteSpace(characteristic) || analysis == null)
            return false;

        if (!_results.TryGetValue(characteristic, out var difficultyDict))
        {
            difficultyDict = [];
            _results[characteristic] = difficultyDict;
        }
        difficultyDict[difficultyRank] = analysis;
        return true;
    }

    /// <summary> Retrieves a specific analysis by characteristic and difficulty rank </summary>
    public DifficultyAnalysis? GetAnalysis(string characteristic, BeatmapDifficultyRank difficultyRank)
    {
        if (string.IsNullOrWhiteSpace(characteristic)) return null;
        return _results.TryGetValue(characteristic, out var difficultyDict) && difficultyDict.TryGetValue(difficultyRank, out var analysis)
            ? analysis
            : null;
    }

    /// <summary> Retrieves all analysis results for a specific characteristic </summary>
    public IReadOnlyCollection<DifficultyAnalysis> GetAnalysisByCharacteristic(string characteristic)
    {
        if (string.IsNullOrWhiteSpace(characteristic)) return [];
        return _results.TryGetValue(characteristic, out var difficultyDict)
            ? (IReadOnlyCollection<DifficultyAnalysis>)[.. difficultyDict.Values]
            : [];
    }

    /// <summary> Retrieves all analysis results across all characteristics and difficulties </summary>
    public IReadOnlyCollection<DifficultyAnalysis> GetAllAnalyses()
    {
        return _results.Values.SelectMany(difficultyDict => difficultyDict.Values).ToList();
    }
}

/// <summary> Performs analysis on a mapset </summary>
public static class MapAnalyser
{
    /// <summary> Performs analysis on every difficulty of every characteristic for the mapset </summary>
    public static async Task<MapAnalysis> AnalyseAsync(SongInfo songData, AudioInfo? audioData = null)
    {
        ArgumentNullException.ThrowIfNull(songData);
        MapAnalysis analysis = new(songData);

        var tasks = songData.DifficultyBeatmaps.Select(async diffInfo =>
        {
            BPMContext bpmContext = audioData == null
                ? BPMContext.CreateBPMContext(songData.Song.BPM, diffInfo.DifficultyData.RawBPMEvents, songData.SongTimeOffset)
                : audioData.ToBPMContext(songData.Song.BPM, songData.SongTimeOffset);

            MapObjects objects = MapObjectsFromData(diffInfo.DifficultyData, bpmContext);
            BotState botState = await Task.Run(() => MapProcessor.Run(objects, bpmContext));

            DifficultyAnalysis diffAnalysis = new(diffInfo.DifficultyData, bpmContext, botState);
            analysis.AddAnalysis(diffInfo.Characteristic, diffInfo.Rank, diffAnalysis);
        });

        await Task.WhenAll(tasks);
        return analysis;
    }

    /// <summary> Returns a diff analysis of a single map difficulty given json strings for info and a difficulty </summary>
    public static DifficultyAnalysis AnalyseSingleDifficultyFromJson(string info, string difficulty, AudioInfo? audioData = null)
    {
        if (string.IsNullOrWhiteSpace(info) || string.IsNullOrWhiteSpace(difficulty))
            throw new ArgumentException("Info and difficulty cannot be null or empty.");

        SongInfo songInfo = BeatmapLoader.LoadSongInfoFromString(info);
        DifficultyData data = BeatmapLoader.LoadDifficultyFromString(difficulty);
        return AnalyseSingleDifficulty(data, songInfo.Song.BPM, songInfo.SongTimeOffset, audioData);
    }

    /// <summary> Analyses a single difficulty given the deserialized song data and difficulty data objects </summary>
    public static DifficultyAnalysis AnalyseSingleDifficulty(DifficultyData difficultyData, float BPM, float songTimeOffset, AudioInfo? audioData = null)
    {
        ArgumentNullException.ThrowIfNull(difficultyData);

        BPMContext bpmContext = audioData == null
            ? BPMContext.CreateBPMContext(BPM, difficultyData.RawBPMEvents, songTimeOffset)
            : audioData.ToBPMContext(BPM, songTimeOffset);

        MapObjects objects = MapObjectsFromData(difficultyData, bpmContext);
        BotState state = MapProcessor.Run(objects, bpmContext);

        return new DifficultyAnalysis(difficultyData, bpmContext, state);
    }

    /// <summary> Setups up DifficultyData in format required by processing </summary>
    public static MapObjects MapObjectsFromData(DifficultyData data, BPMContext bpmContext)
    {
        List<Note> notes = data.Notes.Select(x => { x.MS = bpmContext.ToRealTime(x.B) * 1000; return x; }).ToList();
        List<Bomb> bombs = data.Bombs.Select(x => { x.MS = bpmContext.ToRealTime(x.B) * 1000; return x; }).ToList();
        List<Obstacle> obstacles = data.Obstacles.Select(x => { x.MS = bpmContext.ToRealTime(x.B) * 1000; return x; }).ToList();
        List<Arc> arcs = data.Arcs.Select(x => { x.MS = bpmContext.ToRealTime(x.B) * 1000; x.TMS = bpmContext.ToRealTime(x.TB) * 1000; return x; }).ToList();
        List<Chain> chains = data.Chains.Select(x => { x.MS = bpmContext.ToRealTime(x.B) * 1000; x.TMS = bpmContext.ToRealTime(x.TB) * 1000; return x; }).ToList();
        return new MapObjects(notes, bombs, obstacles, arcs, chains);
    }
}