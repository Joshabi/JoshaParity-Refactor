using JoshaParser.Data.Beatmap;

namespace JoshaParity.Processors;

/// <summary> Handles tracking of active obstacles (walls) for the bot acting like a wall context </summary>
public class WallBuffer
{
    public const float WallExpiryOffset = 0.2f;
    public const float ReturnToNeutralThreshold = 0.5f;

    public List<Obstacle> ActiveWalls { get; private set; } = [];
    public float LastDodgeInfluence { get; set; } = -float.MaxValue;
    public float LastDuckInfluence { get; set; } = -float.MaxValue;

    /// <summary> Processes a wall and updates active wall context </summary>
    public void Process(Obstacle wall) {
        if (!ActiveWalls.Contains(wall))
            ActiveWalls.Add(wall);
    }

    /// <summary> Processes a list of walls and updates active wall context </summary>
    public void BatchProcess(IEnumerable<Obstacle> walls) {
        foreach (Obstacle wall in walls)
            Process(wall);
    }

    /// <summary> Removes walls that have expired </summary>
    public void RemoveExpired(float currentTime) => ActiveWalls.RemoveAll(wall => wall.B + wall.D + WallExpiryOffset < currentTime);

    /// <summary> Returns grid spaces without active walls </summary>
    public List<(int x, int y)> GetAvailableGridSpaces(float currentTime)
    {
        RemoveExpired(currentTime);
        List<(int x, int y)> availableGridSpaces = [];
        for (int x = 0; x <= 3; x++)
        {
            for (int y = 0; y <= 2; y++)
            {
                if (!IsBlocked(x, y))
                    availableGridSpaces.Add((x, y));
            }
        }
        return availableGridSpaces;
    }

    /// <summary> Returns true if a grid space is blocked by a wall </summary>
    public bool IsBlocked(int x, int y)
    {
        foreach (var wall in ActiveWalls)
        {
            int startX = Math.Max(0, wall.X);
            int endX = Math.Min(3, wall.X + wall.W - 1);
            int startY = Math.Max(0, wall.Y - 1);
            int endY = Math.Min(2, wall.Y + wall.H - 1);

            if (x >= startX && x <= endX && y >= startY && y <= endY)
                return true;
        }
        return false;
    }

    /// <summary> Clone the wall state </summary>
    public WallBuffer Clone()
    {
        WallBuffer clone = new() {
            ActiveWalls = new List<Obstacle>(this.ActiveWalls)
        };
        return clone;
    }
}
