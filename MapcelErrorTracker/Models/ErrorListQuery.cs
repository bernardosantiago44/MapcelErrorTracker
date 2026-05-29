namespace MapcelErrorTracker.Models;

public class ErrorListQuery
{
    public const int DefaultPageSize = 10;
    private const int MaxPageSize = 50;

    public string? Search { get; init; }
    public string? Program { get; init; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string SortBy { get; set; } = ErrorListSortFields.Importance;
    public string SortDirection { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = DefaultPageSize;

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Search) ||
        !string.IsNullOrWhiteSpace(Program) ||
        !string.IsNullOrWhiteSpace(Status) ||
        !string.IsNullOrWhiteSpace(Priority);

    public int SafePage => Page < 1 ? 1 : Page;

    public int SafePageSize => PageSize switch
    {
        < 1 => DefaultPageSize,
        > MaxPageSize => MaxPageSize,
        _ => PageSize
    };

    public string SafeSortDirection =>
        string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
}