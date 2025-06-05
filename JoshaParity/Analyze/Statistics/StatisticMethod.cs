namespace JoshaParity.Analyze.Statistics;

/// <summary> Interface for statistic methods that can be applied to a collection of numeric data </summary>
public interface IStatisticMethod
{
    string Identifier { get; }
    double Calculate(IEnumerable<double> data);
}

/// <summary> Mean implementation </summary>
public class MeanStatistic : IStatisticMethod
{
    public string Identifier => "Mean";
    public double Calculate(IEnumerable<double> data) => data.Any() ? data.Average() : double.NaN;
}

/// <summary> Median implementation </summary>
public class MedianStatistic : IStatisticMethod
{
    public string Identifier => "Median";
    public double Calculate(IEnumerable<double> data)
    {
        List<double> sorted = data as List<double> ?? [.. data.OrderBy(x => x)];
        int count = sorted.Count;
        return count == 0 ? double.NaN : count % 2 == 0 ? (sorted[(count / 2) - 1] + sorted[count / 2]) / 2.0 : sorted[count / 2];
    }
}

/// <summary> Mode implementation </summary>
public class ModeStatistic : IStatisticMethod
{
    public string Identifier => "Mode";
    public double Calculate(IEnumerable<double> data)
    {
        IGrouping<double, double> grouped = data.GroupBy(x => x)
                          .OrderByDescending(g => g.Count())
                          .ThenBy(g => g.Key)
                          .FirstOrDefault();
        return grouped?.Key ?? double.NaN;
    }
}

/// <summary> Min implementation </summary>
public class MinStatistic : IStatisticMethod
{
    public string Identifier => "Min";
    public double Calculate(IEnumerable<double> data)
    {
        List<double> list = data as List<double> ?? [.. data];
        return list.Count == 0 ? double.NaN : list.Min();
    }
}

/// <summary> Max implementation </summary>
public class MaxStatistic : IStatisticMethod
{
    public string Identifier => "Max";
    public double Calculate(IEnumerable<double> data)
    {
        List<double> list = data as List<double> ?? [.. data];
        return list.Count == 0 ? double.NaN : list.Max();
    }
}

/// <summary> Standard Deviation implementation </summary>
public class StdDevStatistic : IStatisticMethod
{
    public string Identifier => "StdDev";
    public double Calculate(IEnumerable<double> data)
    {
        List<double> list = data as List<double> ?? [.. data];
        int n = list.Count;
        if (n == 0) return double.NaN;
        double mean = list.Average();
        double variance = list.Sum(x => (x - mean) * (x - mean)) / n;
        return Math.Sqrt(variance);
    }
}

/// <summary> Statistics cache key for identifying specific statistics </summary>
public class StatCacheKey(string identifier, HandResult hand, string selectorIdentifier)
{
    public string StatIdentifier { get; set; } = identifier;
    public HandResult Hand { get; set; } = hand;
    public string SelectorIdentifier { get; set; } = selectorIdentifier;
    public override bool Equals(object obj)
    {
        return obj is StatCacheKey other && StatIdentifier == other.StatIdentifier &&
                   Hand == other.Hand &&
                   SelectorIdentifier == other.SelectorIdentifier;
    }
    public override int GetHashCode()
    {
        unchecked {
            int hash = 17;
            hash = (hash * 23) + (StatIdentifier?.GetHashCode() ?? 0);
            hash = (hash * 23) + Hand.GetHashCode();
            hash = (hash * 23) + (SelectorIdentifier?.GetHashCode() ?? 0);
            return hash;
        }
    }
}