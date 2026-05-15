using System.Data;
using MapcelErrorTracker.Exceptions;
using MapcelErrorTracker.Models;
using Microsoft.Data.SqlClient;

namespace MapcelErrorTracker.Services;

public sealed class ErrorOccurrenceMetricService(
    IConfiguration configuration,
    ILogger<ErrorOccurrenceMetricService> logger)
    : BaseService(configuration, logger), IErrorOccurrenceMetricService
{
    private const string SqlSelectSummaryPage = """
        SELECT COUNT(*)
        FROM (
            SELECT h.[errh_err_ID]
            FROM [MapaLocalizadorVisor].[dbo].[ErrorSistemaHistory] AS h
            GROUP BY h.[errh_err_ID]
        ) AS grouped;

        WITH Aggregated AS (
            SELECT h.[errh_err_ID] AS [ErrorId],
                   MIN(h.[errh_Created]) AS [FirstOccurrenceAt],
                   MAX(h.[errh_Created]) AS [LastOccurrenceAt],
                   COUNT_BIG(*) AS [TotalOccurrences],
                   SUM(CASE WHEN h.[errh_Created] >= @oneHourAgo THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS [OccurrencesLast1H],
                   SUM(CASE WHEN h.[errh_Created] >= @twentyFourHoursAgo THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS [OccurrencesLast24H],
                   SUM(CASE WHEN h.[errh_Created] >= @sevenDaysAgo THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS [OccurrencesLast7D]
            FROM [MapaLocalizadorVisor].[dbo].[ErrorSistemaHistory] AS h
            GROUP BY h.[errh_err_ID]
        )
        SELECT [ErrorId],
               [FirstOccurrenceAt],
               [LastOccurrenceAt],
               [TotalOccurrences],
               [OccurrencesLast1H],
               [OccurrencesLast24H],
               [OccurrencesLast7D]
        FROM Aggregated
        ORDER BY [LastOccurrenceAt] DESC, [ErrorId]
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
        """;

    private const string SqlSelectSummaryByErrorId = """
        SELECT h.[errh_err_ID] AS [ErrorId],
               MIN(h.[errh_Created]) AS [FirstOccurrenceAt],
               MAX(h.[errh_Created]) AS [LastOccurrenceAt],
               COUNT_BIG(*) AS [TotalOccurrences],
               SUM(CASE WHEN h.[errh_Created] >= @oneHourAgo THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS [OccurrencesLast1H],
               SUM(CASE WHEN h.[errh_Created] >= @twentyFourHoursAgo THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS [OccurrencesLast24H],
               SUM(CASE WHEN h.[errh_Created] >= @sevenDaysAgo THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS [OccurrencesLast7D]
        FROM [MapaLocalizadorVisor].[dbo].[ErrorSistemaHistory] AS h
        WHERE h.[errh_err_ID] = @errorId
        GROUP BY h.[errh_err_ID];
        """;

    private const string SqlSelectMinuteHistogram = """
        SELECT CAST(CASE WHEN EXISTS (
            SELECT 1
            FROM [MapaLocalizadorVisor].[dbo].[ErrorSistemaHistory] AS existing
            WHERE existing.[errh_err_ID] = @errorId
        ) THEN 1 ELSE 0 END AS bit) AS [HasOccurrences];

        SELECT CONVERT(int, DATEDIFF_BIG(SECOND, @from, h.[errh_Created]) / 60) AS [BucketIndex],
               COUNT_BIG(*) AS [Occurrences]
        FROM [MapaLocalizadorVisor].[dbo].[ErrorSistemaHistory] AS h
        WHERE h.[errh_err_ID] = @errorId
          AND h.[errh_Created] >= @from
          AND h.[errh_Created] < @to
        GROUP BY CONVERT(int, DATEDIFF_BIG(SECOND, @from, h.[errh_Created]) / 60)
        ORDER BY [BucketIndex];
        """;

    private const string SqlSelectHourlyHistogram = """
        SELECT CAST(CASE WHEN EXISTS (
            SELECT 1
            FROM [MapaLocalizadorVisor].[dbo].[ErrorSistemaHistory] AS existing
            WHERE existing.[errh_err_ID] = @errorId
        ) THEN 1 ELSE 0 END AS bit) AS [HasOccurrences];

        SELECT CONVERT(int, DATEDIFF_BIG(SECOND, @from, h.[errh_Created]) / 3600) AS [BucketIndex],
               COUNT_BIG(*) AS [Occurrences]
        FROM [MapaLocalizadorVisor].[dbo].[ErrorSistemaHistory] AS h
        WHERE h.[errh_err_ID] = @errorId
          AND h.[errh_Created] >= @from
          AND h.[errh_Created] < @to
        GROUP BY CONVERT(int, DATEDIFF_BIG(SECOND, @from, h.[errh_Created]) / 3600)
        ORDER BY [BucketIndex];
        """;

    private const string SqlSelectDailyHistogram = """
        SELECT CAST(CASE WHEN EXISTS (
            SELECT 1
            FROM [MapaLocalizadorVisor].[dbo].[ErrorSistemaHistory] AS existing
            WHERE existing.[errh_err_ID] = @errorId
        ) THEN 1 ELSE 0 END AS bit) AS [HasOccurrences];

        SELECT CONVERT(int, DATEDIFF_BIG(SECOND, @from, h.[errh_Created]) / 86400) AS [BucketIndex],
               COUNT_BIG(*) AS [Occurrences]
        FROM [MapaLocalizadorVisor].[dbo].[ErrorSistemaHistory] AS h
        WHERE h.[errh_err_ID] = @errorId
          AND h.[errh_Created] >= @from
          AND h.[errh_Created] < @to
        GROUP BY CONVERT(int, DATEDIFF_BIG(SECOND, @from, h.[errh_Created]) / 86400)
        ORDER BY [BucketIndex];
        """;

    private const string SqlSelectWeeklyHistogram = """
        SELECT CAST(CASE WHEN EXISTS (
            SELECT 1
            FROM [MapaLocalizadorVisor].[dbo].[ErrorSistemaHistory] AS existing
            WHERE existing.[errh_err_ID] = @errorId
        ) THEN 1 ELSE 0 END AS bit) AS [HasOccurrences];

        SELECT CONVERT(int, DATEDIFF_BIG(SECOND, @from, h.[errh_Created]) / 604800) AS [BucketIndex],
               COUNT_BIG(*) AS [Occurrences]
        FROM [MapaLocalizadorVisor].[dbo].[ErrorSistemaHistory] AS h
        WHERE h.[errh_err_ID] = @errorId
          AND h.[errh_Created] >= @from
          AND h.[errh_Created] < @to
        GROUP BY CONVERT(int, DATEDIFF_BIG(SECOND, @from, h.[errh_Created]) / 604800)
        ORDER BY [BucketIndex];
        """;

    public async Task<ErrorOccurrenceSummaryPageDto> GetSummaryPageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ValidatePagination(page, pageSize);

        var now = DateTime.Now;
        var items = new List<ErrorOccurrenceSummaryDto>();

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(SqlSelectSummaryPage, connection);
            command.CommandType = CommandType.Text;
            AddWindowParameters(command, now);
            command.Parameters.Add(new SqlParameter("@offset", SqlDbType.Int) { Value = (page - 1) * pageSize });
            command.Parameters.Add(new SqlParameter("@pageSize", SqlDbType.Int) { Value = pageSize });

            var totalRecords = 0;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                totalRecords = reader.GetInt32(0);
            }

            await reader.NextResultAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(MapSummary(reader, now));
            }

            return new ErrorOccurrenceSummaryPageDto
            {
                GeneratedAt = now,
                Page = page,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = totalRecords == 0 ? 0 : (int)Math.Ceiling(totalRecords / (double)pageSize),
                Items = items
            };
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to load error occurrence summary page.");
            throw;
        }
    }

    public async Task<ErrorOccurrenceSummaryDto> GetSummaryAsync(
        long errorId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(errorId);

        var now = DateTime.Now;

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(SqlSelectSummaryByErrorId, connection);
            command.CommandType = CommandType.Text;
            command.Parameters.Add(new SqlParameter("@errorId", SqlDbType.BigInt) { Value = errorId });
            AddWindowParameters(command, now);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new NotFoundException(nameof(ErrorOccurrenceSummaryDto));
            }

            return MapSummary(reader, now);
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to load occurrence summary for error {ErrorId}.", errorId);
            throw;
        }
    }

    public async Task<ErrorOccurrenceHistogramDto> GetHistogramAsync(
        long errorId,
        DateTime from,
        DateTime to,
        string bucket,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(errorId);

        var normalizedBucket = NormalizeBucket(bucket, from, to);
        var bucketCount = GetBucketCount(from, to, normalizedBucket);
        var sql = normalizedBucket switch
        {
            "minute" => SqlSelectMinuteHistogram,
            "hour" => SqlSelectHourlyHistogram,
            "day" => SqlSelectDailyHistogram,
            "week" => SqlSelectWeeklyHistogram,
            _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "Unsupported bucket.")
        };
        var countsByBucket = new Dictionary<int, long>();

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);
            command.CommandType = CommandType.Text;
            command.Parameters.Add(new SqlParameter("@errorId", SqlDbType.BigInt) { Value = errorId });
            command.Parameters.Add(new SqlParameter("@from", SqlDbType.DateTime) { Value = from });
            command.Parameters.Add(new SqlParameter("@to", SqlDbType.DateTime) { Value = to });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || !reader.GetBoolean(reader.GetOrdinal("HasOccurrences")))
            {
                throw new NotFoundException(nameof(ErrorOccurrenceHistogramDto));
            }

            await reader.NextResultAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                countsByBucket[reader.GetInt32(reader.GetOrdinal("BucketIndex"))] =
                    reader.GetInt64(reader.GetOrdinal("Occurrences"));
            }

            return new ErrorOccurrenceHistogramDto
            {
                ErrorId = errorId,
                From = from,
                To = to,
                Bucket = normalizedBucket,
                Buckets = BuildHistogramBuckets(from, to, normalizedBucket, bucketCount, countsByBucket)
            };
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to load occurrence histogram for error {ErrorId}.", errorId);
            throw;
        }
    }

    private ErrorOccurrenceSummaryDto MapSummary(SqlDataReader reader, DateTime now)
    {
        var firstOccurrenceAt = reader.GetDateTime(reader.GetOrdinal("FirstOccurrenceAt"));
        var lastOccurrenceAt = reader.GetDateTime(reader.GetOrdinal("LastOccurrenceAt"));
        var totalOccurrences = reader.GetInt64(reader.GetOrdinal("TotalOccurrences"));
        var occurrencesLast1H = reader.GetInt64(reader.GetOrdinal("OccurrencesLast1H"));
        var occurrencesLast24H = reader.GetInt64(reader.GetOrdinal("OccurrencesLast24H"));
        var occurrencesLast7D = reader.GetInt64(reader.GetOrdinal("OccurrencesLast7D"));
        var classification = ErrorHeatClassifier.Classify(
            new ErrorHeatClassifierInput
            {
                FirstSeenAt = firstOccurrenceAt,
                LastSeenAt = lastOccurrenceAt,
                TotalOccurrences = totalOccurrences,
                OccurrencesLast1H = occurrencesLast1H,
                OccurrencesLast24H = occurrencesLast24H,
                OccurrencesLast7D = occurrencesLast7D
            },
            now);

        return new ErrorOccurrenceSummaryDto
        {
            ErrorId = reader.GetInt64(reader.GetOrdinal("ErrorId")),
            FirstOccurrenceAt = firstOccurrenceAt,
            LastOccurrenceAt = lastOccurrenceAt,
            TotalOccurrences = totalOccurrences,
            OccurrencesLast1H = occurrencesLast1H,
            OccurrencesLast24H = occurrencesLast24H,
            OccurrencesLast7D = occurrencesLast7D,
            AgeHours = classification.AgeHours,
            FreshnessScore = classification.FreshnessScore,
            FrequencyScore = classification.FrequencyScore,
            AgePenalty = classification.AgePenalty,
            HeatScore = classification.HeatScore,
            CalculatedPriority = classification.CalculatedPriority.ToString()
        };
    }

    private static void AddWindowParameters(SqlCommand command, DateTime now)
    {
        command.Parameters.Add(new SqlParameter("@oneHourAgo", SqlDbType.DateTime) { Value = now.AddHours(-1) });
        command.Parameters.Add(new SqlParameter("@twentyFourHoursAgo", SqlDbType.DateTime) { Value = now.AddHours(-24) });
        command.Parameters.Add(new SqlParameter("@sevenDaysAgo", SqlDbType.DateTime) { Value = now.AddDays(-7) });
    }

    private static void ValidatePagination(int page, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(page);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        if (pageSize > IErrorOccurrenceMetricService.MaxSummaryPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                $"Page size cannot exceed {IErrorOccurrenceMetricService.MaxSummaryPageSize}.");
        }

        if ((long)(page - 1) * pageSize > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, "Requested page offset is too large.");
        }
    }

    private static string NormalizeBucket(string bucket, DateTime from, DateTime to)
    {
        var normalized = bucket.Trim().ToLowerInvariant();

        if (normalized == "auto")
        {
            return ResolveAutoBucket(from, to);
        }

        return normalized is "minute" or "hour" or "day" or "week"
            ? normalized
            : throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "Bucket must be 'auto', 'minute', 'hour', 'day', or 'week'.");
    }

    private static string ResolveAutoBucket(DateTime from, DateTime to)
    {
        var range = to - from;

        if (range.TotalHours <= 6)
        {
            return "minute";
        }

        if (range.TotalHours <= 48)
        {
            return "hour";
        }

        return range.TotalDays <= 90 ? "day" : "week";
    }

    private static int GetBucketCount(DateTime from, DateTime to, string bucket)
    {
        if (from >= to)
        {
            throw new ArgumentOutOfRangeException(nameof(to), to, "'to' must be later than 'from'.");
        }

        var totalBuckets = GetTotalBuckets(from, to, bucket);

        if (totalBuckets > IErrorOccurrenceMetricService.MaxHistogramBuckets)
        {
            throw new ArgumentOutOfRangeException(
                nameof(to),
                to,
                $"Histogram cannot exceed {IErrorOccurrenceMetricService.MaxHistogramBuckets} buckets.");
        }

        return (int)totalBuckets;
    }

    private static IReadOnlyList<ErrorOccurrenceHistogramBucketDto> BuildHistogramBuckets(
        DateTime from,
        DateTime to,
        string bucket,
        int bucketCount,
        IReadOnlyDictionary<int, long> countsByBucket)
    {
        var buckets = new List<ErrorOccurrenceHistogramBucketDto>(bucketCount);

        for (var index = 0; index < bucketCount; index++)
        {
            var bucketFrom = AddBuckets(from, bucket, index);
            var bucketTo = AddBuckets(bucketFrom, bucket, 1);

            buckets.Add(new ErrorOccurrenceHistogramBucketDto
            {
                From = bucketFrom,
                To = bucketTo > to ? to : bucketTo,
                Occurrences = countsByBucket.GetValueOrDefault(index)
            });
        }

        return buckets;
    }

    private static double GetTotalBuckets(DateTime from, DateTime to, string bucket)
    {
        var range = to - from;

        return bucket switch
        {
            "minute" => Math.Ceiling(range.TotalMinutes),
            "hour" => Math.Ceiling(range.TotalHours),
            "day" => Math.Ceiling(range.TotalDays),
            "week" => Math.Ceiling(range.TotalDays / 7d),
            _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "Unsupported bucket.")
        };
    }

    private static DateTime AddBuckets(DateTime value, string bucket, int count)
    {
        return bucket switch
        {
            "minute" => value.AddMinutes(count),
            "hour" => value.AddHours(count),
            "day" => value.AddDays(count),
            "week" => value.AddDays(count * 7),
            _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "Unsupported bucket.")
        };
    }
}
