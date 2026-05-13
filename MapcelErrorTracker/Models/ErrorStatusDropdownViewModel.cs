namespace MapcelErrorTracker.Models;

public class ErrorStatusDropdownViewModel
{
    public long ErrorId { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public ErrorStatus CurrentStatus { get; set; }
    public string ReturnUrl { get; set; } = string.Empty;
    public string ActionName { get; set; } = "UpdateStatusFromList";
    public string ControllerName { get; set; } = "Errors";

    public string ComponentId => $"error-status-{ErrorId}";
    public IReadOnlyList<ErrorStatus> Options { get; set; } = Enum.GetValues<ErrorStatus>();
}
