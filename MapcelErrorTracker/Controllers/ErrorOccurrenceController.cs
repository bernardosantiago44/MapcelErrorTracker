using MapcelErrorTracker.Exceptions;
using MapcelErrorTracker.Models;
using MapcelErrorTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace MapcelErrorTracker.Controllers;

[ApiController]
[Produces("application/json")]
[Route("errors")]
public sealed class ErrorOccurrenceController(
    IErrorOccurrenceMetricService service,
    ILogger<ErrorOccurrenceController> logger) : ControllerBase
{
    [HttpGet("occurrences/summary")]
    public async Task<ActionResult<ErrorOccurrenceSummaryPageDto>> GetSummary(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = ValidatePagination(page, pageSize);
        if (validationErrors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(validationErrors));
        }

        try
        {
            return Ok(await service.GetSummaryPageAsync(page, pageSize, cancellationToken));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to load occurrence summary page.");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{errorId:long}/occurrences/summary")]
    public async Task<ActionResult<ErrorOccurrenceSummaryDto>> GetSummaryByErrorId(
        long errorId,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = ValidateErrorId(errorId);
        if (validationErrors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(validationErrors));
        }

        try
        {
            return Ok(await service.GetSummaryAsync(errorId, cancellationToken));
        }
        catch (NotFoundException)
        {
            return NotFound("Occurrence summary for error " + errorId + " not found.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to load occurrence summary for error {ErrorId}.", errorId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{errorId:long}/occurrences/histogram")]
    public async Task<ActionResult<ErrorOccurrenceHistogramDto>> GetHistogram(
        long errorId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? bucket,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = ValidateHistogram(errorId, from, to, bucket);
        if (validationErrors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(validationErrors));
        }

        try
        {
            return Ok(await service.GetHistogramAsync(errorId, from!.Value, to!.Value, bucket!, cancellationToken));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            logger.LogWarning(exception, "Invalid histogram request for error {ErrorId}.", errorId);
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "request"] = [exception.Message]
            }));
        }
        catch (NotFoundException)
        {
            return NotFound("Occurrence histogram for error " + errorId + " not found.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to load occurrence histogram for error {ErrorId}.", errorId);
            return StatusCode(500, "Internal server error");
        }
    }

    private static Dictionary<string, string[]> ValidatePagination(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page <= 0)
        {
            errors["page"] = ["page must be greater than zero."];
        }

        if (pageSize is <= 0 or > IErrorOccurrenceMetricService.MaxSummaryPageSize)
        {
            errors["pageSize"] =
            [
                "pageSize must be between 1 and " +
                IErrorOccurrenceMetricService.MaxSummaryPageSize +
                "."
            ];
        }

        if (page > 0 && pageSize > 0 && (long)(page - 1) * pageSize > int.MaxValue)
        {
            errors["page"] = ["The requested page offset is too large."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateErrorId(long errorId)
    {
        var errors = new Dictionary<string, string[]>();

        if (errorId <= 0)
        {
            errors["errorId"] = ["errorId must be greater than zero."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateHistogram(
        long errorId,
        DateTime? from,
        DateTime? to,
        string? bucket)
    {
        var errors = ValidateErrorId(errorId);

        if (from is null)
        {
            errors["from"] = ["from is required."];
        }

        if (to is null)
        {
            errors["to"] = ["to is required."];
        }

        if (from is not null && to is not null && from >= to)
        {
            errors["to"] = ["to must be later than from."];
        }

        if (string.IsNullOrWhiteSpace(bucket))
        {
            errors["bucket"] = ["bucket is required."];
        }
        else if (!IsSupportedBucket(bucket))
        {
            errors["bucket"] = ["bucket must be 'hour' or 'day'."];
        }

        if (from is null ||
            to is null ||
            string.IsNullOrWhiteSpace(bucket) ||
            !IsSupportedBucket(bucket)) return errors;
        var totalBuckets = string.Equals(bucket, "hour", StringComparison.OrdinalIgnoreCase)
            ? Math.Ceiling((to.Value - from.Value).TotalHours)
            : Math.Ceiling((to.Value - from.Value).TotalDays);

        if (totalBuckets > IErrorOccurrenceMetricService.MaxHistogramBuckets)
        {
            errors["bucket"] =
            [
                "The requested range produces more than " +
                IErrorOccurrenceMetricService.MaxHistogramBuckets +
                " buckets."
            ];
        }

        return errors;
    }

    private static bool IsSupportedBucket(string bucket)
    {
        return string.Equals(bucket, "hour", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(bucket, "day", StringComparison.OrdinalIgnoreCase);
    }
}
