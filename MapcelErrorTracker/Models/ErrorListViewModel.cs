namespace MapcelErrorTracker.Models;

public class ErrorListViewModel
{
    public ErrorListQuery Query { get; init; } = new();
    public IReadOnlyList<ErrorItem> Errors { get; init; } = [];
    public IReadOnlyList<string> Companies { get; init; } = [];
    public IReadOnlyList<string> Programs { get; init; } = [];
    public int TotalRecords { get; init; }
    public int FilteredRecords { get; init; }
    public int CurrentPage { get; init; } = 1;
    public int PageSize { get; init; } = ErrorListQuery.DefaultPageSize;
    public bool IsLoading { get; init; }
    public string? ErrorMessage { get; init; }

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
