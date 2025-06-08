using JoshaParity.Data;
using JoshaParity.Utils;
using JoshaParser.Data.Beatmap;
using System.Diagnostics;
using System.Numerics;

namespace JoshaParity.Processors;

/// <summary> Handles tracking of bombs and their influence on parity </summary>
public class BombBuffer
{
    private List<Bomb> _activeBombs = [];

    /// <summary> Creates a clone of the current bomb buffer </summary>
    public BombBuffer Clone()
    {
        return new BombBuffer
        {
            _activeBombs = [.. _activeBombs]
        };
    }

    /// <summary> Processes a bomb and adds it to active bombs </summary>
    public void Process(Bomb bomb)
    {
        if (_activeBombs.Contains(bomb)) return;
        _activeBombs.Add(bomb);
    }

    /// <summary> Gets all bombs that could be relevant to a note </summary>
    public List<Bomb> GetRelevantBombs(float nextSwingMS, float lastSwingMS)
    {
        // Get ALL bombs between the last swing and the next swing
        List<Bomb> bombs = [.. _activeBombs
            .Where(b => b.MS >= lastSwingMS && b.MS <= nextSwingMS)
            .OrderBy(b => b.MS)];

        return bombs;
    }
}