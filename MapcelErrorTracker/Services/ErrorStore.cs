using MapcelErrorTracker.Models;

namespace MapcelErrorTracker.Services;

public class ErrorStore
{
    private readonly List<ErrorItem> _errors;

    public ErrorStore()
    {
        _errors = SeedData();
    }

    public List<ErrorItem> GetAll() => _errors;

    public ErrorItem? GetById(int id) => _errors.FirstOrDefault(e => e.Id == id);

    public void UpdateStatus(int id, ErrorStatus status)
    {
        var error = GetById(id);
        if (error is null) return;
        error.Status = status;
        error.ActivityLog.Add(new ActivityLogEntry
        {
            Timestamp = DateTime.UtcNow,
            User = "dev@local",
            Message = $"Status changed to {status}"
        });
    }

    public void UpdatePriority(int id, ErrorPriority priority)
    {
        var error = GetById(id);
        if (error is null) return;
        error.Priority = priority;
        error.ActivityLog.Add(new ActivityLogEntry
        {
            Timestamp = DateTime.UtcNow,
            User = "dev@local",
            Message = $"Priority changed to {priority}"
        });
    }

    private static List<ErrorItem> SeedData()
    {
        var now = DateTime.UtcNow;
        return
        [
            new ErrorItem
            {
                Id = 1,
                Code = "API-5001",
                Program = "Payment Gateway",
                Module = "PaymentController.ProcessPayment",
                Description = "Unhandled exception while processing payment transaction. Transaction rolled back.",
                Priority = ErrorPriority.High,
                Status = ErrorStatus.New,
                Occurrences = 342,
                FirstSeen = now.AddDays(-5),
                LastSeen = now.AddHours(-4),
                ExceptionMessage = "System.TimeoutException: The operation has timed out.",
                StackTrace = "at PaymentGateway.Controllers.PaymentController.ProcessPayment(PaymentRequest req)\r\n   at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.SyncActionResultExecutor.Execute()\r\n   at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeActionMethodAsync()\r\n   at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Next(State& next, Scope& scope, Object& state, Boolean& isCompleted)",
                ActivityLog =
                [
                    new() { Timestamp = now.AddDays(-5), User = "System", Message = "Error detected" }
                ]
            },
            new ErrorItem
            {
                Id = 2,
                Code = "WRK-3200",
                Program = "Email Worker",
                Module = "EmailWorker.ProcessMessage",
                Description = "Failed to deserialize email template payload. Malformed JSON body received from upstream queue.",
                Priority = ErrorPriority.Medium,
                Status = ErrorStatus.InReview,
                Occurrences = 56,
                FirstSeen = now.AddDays(-2),
                LastSeen = now.AddHours(-8),
                ExceptionMessage = "Newtonsoft.Json.JsonReaderException: Unexpected character encountered while parsing value: <. Path '', line 0, position 0.",
                StackTrace = "at EmailWorker.ProcessMessage(QueueMessage msg) in /src/Workers/EmailWorker.cs:line 34\r\n   at Newtonsoft.Json.JsonTextReader.ParseValue()\r\n   at QueueProcessor.HandleMessage(String body)",
                ActivityLog =
                [
                    new() { Timestamp = now.AddDays(-2), User = "System", Message = "Error detected" },
                    new() { Timestamp = now.AddDays(-1).AddHours(-21), User = "Sarah Chen", Message = "Status changed to InReview" },
                    new() { Timestamp = now.AddDays(-1).AddHours(-20).AddMinutes(-55), User = "Sarah Chen", Message = "Assigned to backend team" }
                ]
            },
            new ErrorItem
            {
                Id = 3,
                Code = "INT-7010",
                Program = "CRM Integration",
                Module = "CrmSyncService.PushContact",
                Description = "CRM API rejected contact sync request with 422 Unprocessable Entity.",
                Priority = ErrorPriority.High,
                Status = ErrorStatus.New,
                Occurrences = 128,
                FirstSeen = now.AddDays(-3),
                LastSeen = now.AddHours(-5),
                ExceptionMessage = "HttpRequestException: Response status code does not indicate success: 422 (Unprocessable Entity).",
                StackTrace = "at CrmIntegration.Services.CrmSyncService.PushContact(ContactDto contact) in /src/Services/CrmSyncService.cs:line 89\r\n   at System.Net.Http.HttpClient.SendAsync(HttpRequestMessage request)\r\n   at CrmIntegration.Workers.SyncWorker.RunAsync(CancellationToken ct)",
                ActivityLog =
                [
                    new() { Timestamp = now.AddDays(-3), User = "System", Message = "Error detected" }
                ]
            },
            new ErrorItem
            {
                Id = 4,
                Code = "API-4040",
                Program = "User Service",
                Module = "UserController.GetProfile",
                Description = "User profile not found in the database. Possible data migration issue.",
                Priority = ErrorPriority.High,
                Status = ErrorStatus.Postponed,
                Occurrences = 89,
                FirstSeen = now.AddDays(-10),
                LastSeen = now.AddHours(-20),
                ExceptionMessage = "KeyNotFoundException: User with id '44f2c' was not found.",
                StackTrace = "at UserService.Controllers.UserController.GetProfile(String userId) in /src/Controllers/UserController.cs:line 55\r\n   at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.SyncActionResultExecutor.Execute()",
                ActivityLog =
                [
                    new() { Timestamp = now.AddDays(-10), User = "System", Message = "Error detected" },
                    new() { Timestamp = now.AddDays(-9), User = "dev@local", Message = "Status changed to Postponed" }
                ]
            },
            new ErrorItem
            {
                Id = 5,
                Code = "WRK-1500",
                Program = "Report Generator",
                Module = "ReportBuilder.GeneratePdf",
                Description = "PDF generation failed due to missing font file in deployment package.",
                Priority = ErrorPriority.Medium,
                Status = ErrorStatus.New,
                Occurrences = 12,
                FirstSeen = now.AddDays(-1),
                LastSeen = now.AddHours(-9),
                ExceptionMessage = "IOException: Could not find file '/app/fonts/Arial.ttf'.",
                StackTrace = "at ReportGenerator.ReportBuilder.GeneratePdf(ReportRequest req) in /src/ReportBuilder.cs:line 112\r\n   at System.IO.FileStream.ValidateFileHandle(SafeFileHandle fileHandle)",
                ActivityLog =
                [
                    new() { Timestamp = now.AddDays(-1), User = "System", Message = "Error detected" }
                ]
            },
            new ErrorItem
            {
                Id = 6,
                Code = "API-4220",
                Program = "Auth Service",
                Module = "TokenService.ValidateJwt",
                Description = "JWT validation failed due to expired signing certificate. All auth requests are rejected.",
                Priority = ErrorPriority.Low,
                Status = ErrorStatus.Resolved,
                Occurrences = 8,
                FirstSeen = now.AddDays(-10),
                LastSeen = now.AddDays(-4),
                ExceptionMessage = "SecurityTokenExpiredException: IDX10223: Lifetime validation failed. The token is expired.",
                StackTrace = "at AuthService.Services.TokenService.ValidateJwt(String token) in /src/Services/TokenService.cs:line 67\r\n   at Microsoft.IdentityModel.Tokens.Validators.ValidateLifetime()\r\n   at System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.ValidateToken()",
                ActivityLog =
                [
                    new() { Timestamp = now.AddDays(-10), User = "System", Message = "Error detected" },
                    new() { Timestamp = now.AddDays(-8), User = "dev@local", Message = "Status changed to InReview" },
                    new() { Timestamp = now.AddDays(-5), User = "dev@local", Message = "Certificate renewed and redeployed" },
                    new() { Timestamp = now.AddDays(-4), User = "dev@local", Message = "Status changed to Resolved" }
                ]
            },
            new ErrorItem
            {
                Id = 7,
                Code = "INT-8080",
                Program = "Webhook Handler",
                Module = "WebhookController.Receive",
                Description = "Incoming webhook signature verification failed. Possible replay attack or misconfigured secret.",
                Priority = ErrorPriority.Medium,
                Status = ErrorStatus.New,
                Occurrences = 23,
                FirstSeen = now.AddDays(-2),
                LastSeen = now.AddHours(-6),
                ExceptionMessage = "InvalidOperationException: HMAC-SHA256 signature mismatch on incoming webhook payload.",
                StackTrace = "at WebhookHandler.Controllers.WebhookController.Receive(WebhookPayload payload) in /src/Controllers/WebhookController.cs:line 41\r\n   at System.Security.Cryptography.HMACSHA256.ComputeHash(Byte[] buffer)",
                ActivityLog =
                [
                    new() { Timestamp = now.AddDays(-2), User = "System", Message = "Error detected" }
                ]
            },
            new ErrorItem
            {
                Id = 8,
                Code = "WRK-9900",
                Program = "Data Pipeline",
                Module = "EtlJob.TransformBatch",
                Description = "ETL batch transformation produced null output on record #1042. Downstream write skipped.",
                Priority = ErrorPriority.Low,
                Status = ErrorStatus.InReview,
                Occurrences = 4,
                FirstSeen = now.AddDays(-3),
                LastSeen = now.AddDays(-1),
                ExceptionMessage = "NullReferenceException: Object reference not set to an instance of an object.",
                StackTrace = "at DataPipeline.Jobs.EtlJob.TransformBatch(IEnumerable`1 records) in /src/Jobs/EtlJob.cs:line 203\r\n   at DataPipeline.Workers.PipelineWorker.RunBatch(BatchContext ctx)",
                ActivityLog =
                [
                    new() { Timestamp = now.AddDays(-3), User = "System", Message = "Error detected" },
                    new() { Timestamp = now.AddDays(-2), User = "dev@local", Message = "Status changed to InReview" }
                ]
            }
        ];
    }
}
