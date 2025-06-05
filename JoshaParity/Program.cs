using JoshaParity.Analyze;
using JoshaParity.Processors;
using JoshaParser.Data.Metadata;
using JoshaParser.Parsers;

class Program
{
    static void Main()
    {
        Console.WriteLine("JoshaParity: RELOADED");
        string mapFolder = "./Maps/BNW";
        if (!Directory.Exists(mapFolder)) { return; }

        // Load mapset
        Beatmap? map = BeatmapLoader.LoadMapFromDirectory(mapFolder);
        if (map is null) { return; }

        Console.WriteLine(map);
        Console.WriteLine("\n\nTime to analyse!\n");

        BeatmapAnalysis result = BeatmapAnalyser.CreateFromBeatmap(map, new MapAnalyserConfig()
        {
            AnalyserConfig = new AnalysisConfig()
            {
                AngleLimit = 180f,
                AngleTolerance = 270f
            },
            PrecalculateAllDifficulties = false
        });

        DifficultyAnalysis? analysis = result.GetAnalysisAsync("standard", BeatmapDifficultyRank.ExpertPlus).GetAwaiter().GetResult();

        if (analysis is not null)
            Console.WriteLine(analysis.ToString());
    }
}