# JoshaParity-Refactor

**Swing Data & Statistics Library for Beat Saber**

[![C#](https://img.shields.io/badge/language-C%23-blue.svg)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/github/license/Joshabi/JoshaParity)](LICENSE)

---

JoshaParity-Refactor is a C# library designed for analysis and retrieving statistics about Beat Saber maps. It can generate predicted swing data, movement data and provide a wide range of statistics—including NPS/SPS, handedness, resets, and more. It can process V2, V3 and V4 maps and has Lazy Cache functionality to only load and analyse when required.

---

## Installation

Clone the repo and build the project or download from Releases. There is a build action with the provided VS 2022 solution that will automatically output the dll's to your Beat Saber directory inside of the /libs/ folder. You may need to adjust the path in the csproj file if its non-standard.

```bash
git clone https://github.com/Joshabi/JoshaParity-Refactor.git
```

---

## Usage

### 1. Loading a Beat Saber Map

You can load a Beat Saber map directory (containing `info.dat` and difficulty files) using the `BeatmapLoader` from the dependency `JoshaParser`:

```csharp
string mapFolder = ".\Maps Folder\Additional Memory";
Beatmap? map = BeatmapLoader.LoadMapFromDirectory(mapFolder);
```

### 2. Creating a Beatmap Analysis Instance

```csharp
BeatmapAnalysis analysis = BeatmapAnalyser.CreateFromBeatmap(map);
```
You can also configure and pass in analysis options:

```csharp
var config = new MapAnalyserConfig() {
  PrecalculateAllDifficulties = true,
  AnalyserConfig = new() {
    AngleTolerance = 235.0f,
    AngleLimit = 135.0f
  }
};
var analysis = BeatmapAnalyser.CreateFromBeatmap(map, config);
```

### 3. Analyzing Difficulties

To get analysis results for a specific difficulty:

```csharp
string characteristic = "Standard"; // or "OneSaber", etc.
BeatmapDifficultyRank rank = BeatmapDifficultyRank.ExpertPlus;

DifficultyAnalysis? difficultyAnalysis = await analysis.GetAnalysisAsync(characteristic, rank);
```

Or, using `DifficultyInfo`:

```csharp
DifficultyInfo diffInfo = ... // from Beatmap.SongData.DifficultyBeatmaps
DifficultyAnalysis? difficultyAnalysis = await analysis.GetAnalysisAsync(diffInfo);
```

### 4. Getting Statistics

On a `DifficultyAnalysis` object, you can query a wide range of statistics:

```csharp
// NPS (Notes Per Second)
double nps = difficultyAnalysis.GetNPS();

// SPS (Swings Per Second)
double sps = difficultyAnalysis.GetSPS();

// Handedness (% of swings per hand)
double rightHand = difficultyAnalysis.GetHandedness(HandResult.Right);

// Swing Type Percentages
double normalSwings = difficultyAnalysis.GetSwingTypePercent(SwingType.Normal);

// Effective BPM (mean, min, max, etc.)
double meanEBPM = difficultyAnalysis.GetEBPMStat(HandResult.Both, new MeanStatistic());
double maxEBPM = difficultyAnalysis.GetEBPMStat(HandResult.Both, new MaxStatistic());

// Angle Change, Repositions, Time Between Swings, etc.
double meanAngleChange = difficultyAnalysis.GetAngleChangeStat(HandResult.Both, new MeanStatistic());
double meanReposition = difficultyAnalysis.GetRepositionStat(HandResult.Both, new MeanStatistic());
```

---

## Dependencies

- **JoshaParser** (https://github.com/Joshabi/JoshaParser):  
  Used for map parsing, metadata, and data structures.

---

## Example Simple Output: Full Workflow

```csharp
using JoshaParser.Parsers;
using JoshaParity.Analyze;

string mapPath = "./Maps/Best Map Ever";
var map = BeatmapLoader.LoadMapFromDirectory(mapPath);
var analysis = BeatmapAnalyser.CreateFromBeatmap(map);

foreach (var diff in map.SongData.DifficultyBeatmaps) {
    var diffAnalysis = await analysis.GetAnalysisAsync(diff);
    Console.WriteLine(diffAnalysis?.ToString());
}
```
