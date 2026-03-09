namespace MapcelErrorTracker.Models;

public enum ErrorPriority { Baja, Media, Alta }
public enum ErrorStatus { New, Postponed, InReview, Resolved }

public class ActivityLogEntry
{
    public DateTime Timestamp { get; set; }
    public string User { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class NotificationContact
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
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

    // Classification & context
    public string Environment { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Subcategory { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public bool? IsReproducible { get; set; }
    public string OwnerArea { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string DestinationSystem { get; set; } = string.Empty;
    public bool BlocksOperation { get; set; }
    public string Process { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();

    // Tracking
    public string CreatedBy { get; set; } = string.Empty;
    public string ModifiedBy { get; set; } = string.Empty;
    public DateTime? LastUpdated { get; set; }
    public DateTime? NextFollowUp { get; set; }
    public string PostponeReason { get; set; } = string.Empty;
    public string LatestComment { get; set; } = string.Empty;
    public string NextStep { get; set; } = string.Empty;

    // Technical IDs
    public string CorrelationId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string FolioNumber { get; set; } = string.Empty;

    // Notifications
    public List<NotificationContact> NotificationContacts { get; set; } = new();
    public int NotificationFrequencyMinutes { get; set; }
    public DateTime? SilencedUntil { get; set; }
    public DateTime? LastNotificationSent { get; set; }

    // Additional / raw data
    public string RequestPayload { get; set; } = string.Empty;
    public string ResponsePayload { get; set; } = string.Empty;
    public string RawHeaders { get; set; } = string.Empty;
    public string AdditionalComments { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}
