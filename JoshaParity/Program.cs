using JoshaParity.Analyze;
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
        SongInfo? song = BeatmapLoader.LoadMapFromDirectory(mapFolder);
        if (song is null) { return; }

        Console.WriteLine(song);
        Console.WriteLine("\n\nTime to analyse!\n");

        MapAnalysis analysis = MapAnalyser.AnalyseAsync(song).Result;
        if (analysis is null) { return; };

        foreach (DifficultyAnalysis analysisResult in analysis.GetAllAnalyses())
        {
            Console.WriteLine(analysisResult);
        }
    }
}