using MapcelErrorTracker.Models;

namespace MapcelErrorTracker.Services;

public sealed record ErrorHeatClassifierInput
{
    public DateTime FirstSeenAt { get; init; }
    public DateTime LastSeenAt { get; init; }
    public long TotalOccurrences { get; init; }
    public long OccurrencesLast1h { get; init; }
    public long OccurrencesLast24h { get; init; }
    public long OccurrencesLast7d { get; init; }
}

public sealed record ErrorHeatClassification
{
    public double AgeHours { get; init; }
    public double FreshnessScore { get; init; }
    public double FrequencyScore { get; init; }
    public double AgePenalty { get; init; }
    public double HeatScore { get; init; }
    public ErrorPriority CalculatedPriority { get; init; }
}

public sealed class ErrorHeatClassifier
{
    private const double HighPriorityThreshold = 50;
    private const double MediumPriorityThreshold = 20;

    public ErrorHeatClassification Classify(ErrorHeatClassifierInput input, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(input);

        var ageHours = Math.Max(0, (now - input.FirstSeenAt).TotalHours);
        var freshnessAge = now - input.LastSeenAt;
        var frequencyScore =
            input.OccurrencesLast1h * 3d +
            input.OccurrencesLast24h +
            input.OccurrencesLast7d * 0.25d;
        var freshnessScore = freshnessAge.TotalMinutes switch
        {
            <= 15 => 30,
            <= 60 => 20,
            <= 360 => 10,
            _ => 0
        };
        var agePenalty = ageHours switch
        {
            > 720 when input.TotalOccurrences < 20 => -35,
            > 168 when input.TotalOccurrences < 10 => -20,
            _ => 0
        };
        var heatScore = frequencyScore + freshnessScore + agePenalty;

        return new ErrorHeatClassification
        {
            AgeHours = Math.Round(ageHours, 2),
            FreshnessScore = freshnessScore,
            FrequencyScore = Math.Round(frequencyScore, 2),
            AgePenalty = agePenalty,
            HeatScore = Math.Round(heatScore, 2),
            CalculatedPriority = CalculatePriority(heatScore)
        };
    }

    private static ErrorPriority CalculatePriority(double heatScore)
    {
        return heatScore switch
        {
            >= HighPriorityThreshold => ErrorPriority.Alta,
            >= MediumPriorityThreshold => ErrorPriority.Media,
            _ => ErrorPriority.Baja
        };
    }
}
