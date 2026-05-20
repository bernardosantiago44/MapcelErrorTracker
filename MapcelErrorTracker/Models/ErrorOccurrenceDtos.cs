namespace MapcelErrorTracker.Models;

public sealed record ErrorOccurrenceSummaryPageDto
{
    public DateTime GeneratedAt { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalRecords { get; init; }
    public int TotalPages { get; init; }
    public IReadOnlyList<ErrorOccurrenceSummaryDto> Items { get; init; } = [];
}

public sealed record ErrorOccurrenceSummaryDto
{
    public long ErrorId { get; init; }
    public DateTime FirstOccurrenceAt { get; init; }
    public DateTime LastOccurrenceAt { get; init; }
    public long TotalOccurrences { get; init; }
    public long OccurrencesLast1H { get; init; }
    public long OccurrencesLast24H { get; init; }
    public long OccurrencesLast7D { get; init; }
    public double AgeHours { get; init; }
    public double FreshnessScore { get; init; }
    public double FrequencyScore { get; init; }
    public double AgePenalty { get; init; }
    public double HeatScore { get; init; }
    public string CalculatedPriority { get; init; } = string.Empty;
}

public sealed record ErrorOccurrenceHistogramDto
{
    public long ErrorId { get; init; }
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public string Bucket { get; init; } = string.Empty;
    public IReadOnlyList<ErrorOccurrenceHistogramBucketDto> Buckets { get; init; } = [];
}

public sealed record ErrorOccurrenceHistogramBucketDto
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public long Occurrences { get; init; }
}

public sealed record OccurrenceTrendChartViewModel
{
    public long ErrorId { get; init; }
    public DateTime FirstSeen { get; init; }
    public DateTime LastSeen { get; init; }
}
