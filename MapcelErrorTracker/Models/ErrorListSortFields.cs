namespace MapcelErrorTracker.Models;

public static class ErrorListSortFields
{
    public const string Status = "status";
    public const string Priority = "priority";
    public const string Company = "company";
    public const string ErrorCode = "code";
    public const string Occurrences = "occurrences";
    public const string Importance = "importance";
    public const string FirstSeen = "firstSeen";
    public const string LastSeen = "lastSeen";

    public static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        Status,
        Priority,
        Company,
        ErrorCode,
        Occurrences,
        Importance,
        FirstSeen,
        LastSeen
    };
}