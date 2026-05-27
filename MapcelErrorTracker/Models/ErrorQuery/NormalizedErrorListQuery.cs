namespace MapcelErrorTracker.Models.ErrorQuery;

public sealed record NormalizedErrorListQuery(
    string? Search,
    string? Program,
    ErrorStatus? Status,
    bool IncludeAllStatuses,
    ErrorPriority? Priority,
    string SortBy,
    string SortDirection,
    int Page,
    int PageSize
);