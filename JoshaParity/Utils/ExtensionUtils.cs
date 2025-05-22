using System.Numerics;
using JoshaParity.Data;
using JoshaParser.Data.Beatmap;

namespace JoshaParity.Utils;

/// <summary> Type Extension utilities </summary>
public static class ExtensionUtils
{
    /// <summary> Converts a Cut Direction, Parity and Hand into a rotation value for the saber </summary>
    public static float ToRotation(this CutDirection dir, Parity parity, Hand hand)
    {
        var rotationDict = parity == Parity.Forehand
            ? ParityUtils.ForehandDict(hand)
            : ParityUtils.BackhandDict(hand);

        return rotationDict.TryGetValue((int)dir, out var angle) ? angle : 0;
    }
    public static float ToRotation(this CutDirection dir, SwingData thisSwing) { return dir.ToRotation(thisSwing.Parity, thisSwing.Hand); }

    /// <summary> Gets the nearest diagonal cut direction for vector </summary>
    public static CutDirection NearestDiagonal(this Vector2 direction)
    {
        Vector2[] diagonals =
        [
            new Vector2(1, 1),   // Top-right
            new Vector2(-1, 1),  // Top-left
            new Vector2(-1, -1), // Bottom-left
            new Vector2(1, -1)   // Bottom-right
        ];

        Vector2 closestDiagonal = Vector2.Zero;
        float maxDot = float.NegativeInfinity;
        foreach (var diagonal in diagonals)
        {
            float dot = direction.X * diagonal.X + direction.Y * diagonal.Y;
            if (dot > maxDot)
            {
                maxDot = dot;
                closestDiagonal = diagonal;
            }
        }

        return SwingUtils.DirectionalVectorToCutDirection[closestDiagonal];
    }
}
