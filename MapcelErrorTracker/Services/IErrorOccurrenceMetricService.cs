using MapcelErrorTracker.Models;

namespace MapcelErrorTracker.Services;

public interface IErrorOccurrenceMetricService
{
    const int MaxSummaryPageSize = 200;
    const int MaxHistogramBuckets = 500;

    Task<ErrorOccurrenceSummaryPageDto> GetSummaryPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ErrorOccurrenceSummaryDto> GetSummaryAsync(
        long errorId,
        CancellationToken cancellationToken);

    Task<ErrorOccurrenceHistogramDto> GetHistogramAsync(
        long errorId,
        DateTime from,
        DateTime to,
        string bucket,
        CancellationToken cancellationToken);
}
