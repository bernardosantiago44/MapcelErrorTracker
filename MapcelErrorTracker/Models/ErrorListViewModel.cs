namespace MapcelErrorTracker.Models;

public class ErrorListQuery
{
    public const int DefaultPageSize = 10;
    private const int MaxPageSize = 50;

    public string? Search { get; set; }
    public string? Company { get; set; }
    public string? Program { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string SortBy { get; set; } = ErrorListSortFields.LastSeen;
    public string SortDirection { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = DefaultPageSize;

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Search) ||
        !string.IsNullOrWhiteSpace(Company) ||
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

public static class ErrorListSortFields
{
    public const string Status = "status";
    public const string Priority = "priority";
    public const string Company = "company";
    public const string ErrorCode = "code";
    public const string Occurrences = "occurrences";
    public const string FirstSeen = "firstSeen";
    public const string LastSeen = "lastSeen";

    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        Status,
        Priority,
        Company,
        ErrorCode,
        Occurrences,
        FirstSeen,
        LastSeen
    };
}

public class ErrorListViewModel
{
    public ErrorListQuery Query { get; set; } = new();
    public IReadOnlyList<ErrorItem> Errors { get; set; } = [];
    public IReadOnlyList<string> Companies { get; set; } = [];
    public IReadOnlyList<string> Programs { get; set; } = [];
    public int TotalRecords { get; set; }
    public int FilteredRecords { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = ErrorListQuery.DefaultPageSize;
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasResults => Errors.Count > 0;
    public bool HasFilters => Query.HasFilters;
    public int TotalPages => FilteredRecords == 0 ? 1 : (int)Math.Ceiling(FilteredRecords / (double)PageSize);
    public int FirstRecord => FilteredRecords == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;
    public int LastRecord => Math.Min(CurrentPage * PageSize, FilteredRecords);
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    public static ErrorListViewModel Error(ErrorListQuery query, string message) => new()
    {
        Query = query,
        ErrorMessage = message
    };
}
