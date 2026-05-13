namespace MapcelErrorTracker.Models;

public enum ErrorPriority { Baja, Media, Alta }
public enum ErrorStatus { Nuevo, Pospuesto, EnRevision, Resuelto, SinAsignar }

public class ActivityLogEntry
{
    public DateTime Timestamp { get; init; }
    public string User { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public class NotificationContact
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class ErrorItem
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string Program { get; init; } = string.Empty;
    public string Module { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ErrorPriority Priority { get; init; }
    public ErrorStatus Status { get; init; }
    public int Occurrences { get; init; }
    public DateTime FirstSeen { get; init; }
    public DateTime LastSeen { get; init; }
    public string ExceptionMessage { get; init; } = string.Empty;
    public string StackTrace { get; init; } = string.Empty;
    public List<ProgrammerUser> AssignedUsers { get; set; } = [];
    public List<ProgrammerUser> AvailableAssignees { get; set; } = [];
    public string Assignee => string.Join(", ", AssignedUsers.Select(user => user.Name));
    public bool IsSilenced { get; init; }
    public List<ActivityLogEntry> ActivityLog { get; init; } = [];

    // Classification & context
    public string Environment { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;
    public string ErrorType { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Subcategory { get; init; } = string.Empty;
    public string Impact { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Frequency { get; init; } = string.Empty;
    public bool? IsReproducible { get; init; }
    public string OwnerArea { get; init; } = string.Empty;
    public string SourceSystem { get; init; } = string.Empty;
    public string DestinationSystem { get; init; } = string.Empty;
    public bool BlocksOperation { get; init; }
    public string Process { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = [];

    // Tracking
    public string CreatedBy { get; init; } = string.Empty;
    public string ModifiedBy { get; init; } = string.Empty;
    public DateTime? LastUpdated { get; init; }
    public DateTime? NextFollowUp { get; init; }
    public string PostponeReason { get; init; } = string.Empty;
    public string LatestComment { get; init; } = string.Empty;
    public string NextStep { get; init; } = string.Empty;

    // Technical IDs
    public string CorrelationId { get; init; } = string.Empty;
    public string RequestId { get; init; } = string.Empty;
    public string FolioNumber { get; init; } = string.Empty;

    // Notifications
    public List<NotificationContact> NotificationContacts { get; init; } = [];
    public int NotificationFrequencyMinutes { get; init; }
    public DateTime? SilencedUntil { get; init; }
    public DateTime? LastNotificationSent { get; init; }

    // Additional / raw data
    public string RequestPayload { get; init; } = string.Empty;
    public string ResponsePayload { get; init; } = string.Empty;
    public string RawHeaders { get; init; } = string.Empty;
    public string AdditionalComments { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
}
