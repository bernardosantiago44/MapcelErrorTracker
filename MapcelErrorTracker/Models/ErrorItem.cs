namespace MapcelErrorTracker.Models;

public enum ErrorPriority { Baja, Media, Alta }
public enum ErrorStatus { New, Postponed, InReview, Resolved }

public class ActivityLogEntry
{
    public DateTime Timestamp { get; set; }
    public string User { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ErrorItem
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Program { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ErrorPriority Priority { get; set; }
    public ErrorStatus Status { get; set; }
    public int Occurrences { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public string ExceptionMessage { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public bool IsSilenced { get; set; }
    public List<ActivityLogEntry> ActivityLog { get; set; } = new();
}
