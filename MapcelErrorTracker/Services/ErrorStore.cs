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
                Company = "Mapcel S.A.",
                Program = "Payment Gateway",
                Module = "PaymentController.ProcessPayment",
                Description = "Unhandled exception while processing payment transaction. Transaction rolled back.",
                Priority = ErrorPriority.Alta,
                Status = ErrorStatus.New,
                Occurrences = 342,
                FirstSeen = now.AddDays(-5),
                LastSeen = now.AddHours(-4),
                ExceptionMessage = "System.TimeoutException: The operation has timed out.",
                StackTrace = "at PaymentGateway.Controllers.PaymentController.ProcessPayment(PaymentRequest req)\r\n   at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.SyncActionResultExecutor.Execute()\r\n   at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.InvokeActionMethodAsync()\r\n   at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Next(State& next, Scope& scope, Object& state, Boolean& isCompleted)",
                Assignee = "Carlos López",
                Environment = "Producción",
                Channel = "API REST",
                ErrorType = "Timeout",
                Category = "Infraestructura",
                Subcategory = "Conectividad",
                Impact = "Alto",
                Severity = "Crítico",
                Frequency = "Recurrente",
                IsReproducible = true,
                OwnerArea = "Pagos",
                SourceSystem = "Payment Gateway",
                DestinationSystem = "Banco Central",
                BlocksOperation = true,
                Process = "Procesamiento de pagos",
                Tags = ["recurrente", "negocio", "integración"],
                CreatedBy = "System",
                ModifiedBy = "Carlos López",
                LastUpdated = now.AddHours(-4),
                NextFollowUp = now.AddDays(1),
                LatestComment = "Se detectó saturación en el proveedor de pagos. Escalar al equipo de infraestructura.",
                NextStep = "Revisar logs del balanceador de carga",
                CorrelationId = "corr-a1b2c3d4-e5f6",
                RequestId = "req-7890abcd",
                NotificationContacts =
                [
                    new() { Name = "Carlos López", Email = "carlos@mapcel.com", Role = "Backend Lead" },
                    new() { Name = "Ana Martínez", Email = "ana@mapcel.com", Role = "SRE" },
                    new() { Name = "Miguel Torres", Email = "miguel@mapcel.com", Role = "Manager" }
                ],
                NotificationFrequencyMinutes = 60,
                LastNotificationSent = now.AddHours(-1),
                RequestPayload = "{ \"amount\": 1500.00, \"currency\": \"MXN\", \"merchantId\": \"MCH-001\" }",
                ResponsePayload = "{ \"error\": \"TIMEOUT\", \"code\": 504 }",
                ActivityLog =
                [
                    new() { Timestamp = now.AddDays(-5), User = "System", Message = "Error detectado" },
                    new() { Timestamp = now.AddDays(-4), User = "Carlos López", Message = "Se revisó el timeout, parece saturación" },
                    new() { Timestamp = now.AddDays(-3), User = "Ana Martínez", Message = "Se escaló al equipo de infraestructura" },
                    new() { Timestamp = now.AddDays(-1), User = "Carlos López", Message = "Persiste el problema tras reiniciar servicios" }
                ]
            },
            new ErrorItem
            {
                Id = 2,
                Code = "WRK-3200",
                Company = "Mapcel S.A.",
                Program = "Email Worker",
                Module = "EmailWorker.ProcessMessage",
                Description = "Failed to deserialize email template payload. Malformed JSON body received from upstream queue.",
                Priority = ErrorPriority.Media,
                Status = ErrorStatus.InReview,
                Occurrences = 56,
                FirstSeen = now.AddDays(-2),
                LastSeen = now.AddHours(-8),
                ExceptionMessage = "Newtonsoft.Json.JsonReaderException: Unexpected character encountered while parsing value: <. Path '', line 0, position 0.",
                StackTrace = "at EmailWorker.ProcessMessage(QueueMessage msg) in /src/Workers/EmailWorker.cs:line 34\r\n   at Newtonsoft.Json.JsonTextReader.ParseValue()\r\n   at QueueProcessor.HandleMessage(String body)",
                Assignee = "Sarah Chen",
                Environment = "Producción",
                Channel = "Cola de mensajes",
                ErrorType = "Deserialización",
                Category = "Integración",
                BlocksOperation = false,
                Process = "Envío de correos",
                Tags = ["intermitente", "integración"],
                CreatedBy = "System",
                ModifiedBy = "Sarah Chen",
                LastUpdated = now.AddDays(-1).AddHours(-20).AddMinutes(-55),
                LatestComment = "Asignado al equipo de backend",
                NextStep = "Validar formato del mensaje upstream",
                CorrelationId = "corr-x9y8z7w6",
                NotificationContacts =
                [
                    new() { Name = "Sarah Chen", Email = "sarah@mapcel.com", Role = "Developer" }
                ],
                NotificationFrequencyMinutes = 120,
                ActivityLog =
                [
                    new() { Timestamp = now.AddDays(-2), User = "System", Message = "Error detectado" },
                    new() { Timestamp = now.AddDays(-1).AddHours(-21), User = "Sarah Chen", Message = "Estado cambiado a En Revisión" },
                    new() { Timestamp = now.AddDays(-1).AddHours(-20).AddMinutes(-55), User = "Sarah Chen", Message = "Asignado al equipo de backend" }
                ]
            },
            new ErrorItem
            {
                Id = 3,
                Code = "INT-7010",
                Company = "Distribuciones Norte",
                Program = "CRM Integration",
                Module = "CrmSyncService.PushContact",
                Description = "CRM API rejected contact sync request with 422 Unprocessable Entity.",
                Priority = ErrorPriority.Alta,
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
                Company = "Mapcel S.A.",
                Program = "User Service",
                Module = "UserController.GetProfile",
                Description = "User profile not found in the database. Possible data migration issue.",
                Priority = ErrorPriority.Alta,
                Status = ErrorStatus.Postponed,
                Occurrences = 89,
                FirstSeen = now.AddDays(-10),
                LastSeen = now.AddHours(-20),
                ExceptionMessage = "KeyNotFoundException: User with id '44f2c' was not found.",
                StackTrace = "at UserService.Controllers.UserController.GetProfile(String userId) in /src/Controllers/UserController.cs:line 55\r\n   at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.SyncActionResultExecutor.Execute()",
                Assignee = "Miguel Ángel Torres",
                Environment = "Producción",
                BlocksOperation = false,
                Process = "Consulta de perfiles",
                PostponeReason = "Esperando migración de datos de usuarios del sistema legacy",
                NextFollowUp = now.AddDays(3),
                CreatedBy = "System",
                ModifiedBy = "dev@local",
                LastUpdated = now.AddDays(-9),
                Tags = ["negocio"],
                ActivityLog =
                [
                    new() { Timestamp = now.AddDays(-10), User = "System", Message = "Error detectado" },
                    new() { Timestamp = now.AddDays(-9), User = "dev@local", Message = "Estado cambiado a Pospuesto" }
                ]
            },
            new ErrorItem
            {
                Id = 5,
                Code = "WRK-1500",
                Company = "Logística Express",
                Program = "Report Generator",
                Module = "ReportBuilder.GeneratePdf",
                Description = "PDF generation failed due to missing font file in deployment package.",
                Priority = ErrorPriority.Media,
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
                Company = "Distribuciones Norte",
                Program = "Auth Service",
                Module = "TokenService.ValidateJwt",
                Description = "JWT validation failed due to expired signing certificate. All auth requests are rejected.",
                Priority = ErrorPriority.Baja,
                Status = ErrorStatus.Resolved,
                Occurrences = 8,
                FirstSeen = now.AddDays(-10),
                LastSeen = now.AddDays(-4),
                ExceptionMessage = "SecurityTokenExpiredException: IDX10223: Lifetime validation failed. The token is expired.",
                StackTrace = "at AuthService.Services.TokenService.ValidateJwt(String token) in /src/Services/TokenService.cs:line 67\r\n   at Microsoft.IdentityModel.Tokens.Validators.ValidateLifetime()\r\n   at System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.ValidateToken()",
                Assignee = "Carlos López",
                Environment = "Producción",
                Category = "Seguridad",
                BlocksOperation = true,
                Process = "Autenticación",
                CreatedBy = "System",
                ModifiedBy = "dev@local",
                LastUpdated = now.AddDays(-4),
                LatestComment = "Certificado renovado y redespleado. Problema resuelto.",
                Tags = ["seguridad"],
                ActivityLog =
                [
                    new() { Timestamp = now.AddDays(-10), User = "System", Message = "Error detectado" },
                    new() { Timestamp = now.AddDays(-8), User = "dev@local", Message = "Estado cambiado a En Revisión" },
                    new() { Timestamp = now.AddDays(-5), User = "dev@local", Message = "Certificado renovado y redespleado" },
                    new() { Timestamp = now.AddDays(-4), User = "dev@local", Message = "Estado cambiado a Resuelto" }
                ]
            },
            new ErrorItem
            {
                Id = 7,
                Code = "INT-8080",
                Company = "Mapcel S.A.",
                Program = "Webhook Handler",
                Module = "WebhookController.Receive",
                Description = "Incoming webhook signature verification failed. Possible replay attack or misconfigured secret.",
                Priority = ErrorPriority.Media,
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
                Company = "Logística Express",
                Program = "Data Pipeline",
                Module = "EtlJob.TransformBatch",
                Description = "ETL batch transformation produced null output on record #1042. Downstream write skipped.",
                Priority = ErrorPriority.Baja,
                Status = ErrorStatus.InReview,
                Occurrences = 4,
                FirstSeen = now.AddDays(-3),
                LastSeen = now.AddDays(-1),
                ExceptionMessage = "NullReferenceException: Object reference not set to an instance of an object.",
                StackTrace = "at DataPipeline.Jobs.EtlJob.TransformBatch(IEnumerable`1 records) in /src/Jobs/EtlJob.cs:line 203\r\n   at DataPipeline.Workers.PipelineWorker.RunBatch(BatchContext ctx)",
                Assignee = "Ana Martínez",
                ActivityLog =
                [
                    new() { Timestamp = now.AddDays(-3), User = "System", Message = "Error detected" },
                    new() { Timestamp = now.AddDays(-2), User = "dev@local", Message = "Status changed to InReview" }
                ]
            },
            new ErrorItem
            {
                Id = 9,
                Code = "API-5001",
                Company = "Distribuciones Norte",
                Program = "Payment Gateway",
                Module = "PaymentController.ProcessPayment",
                Description = "Timeout in payment processing. Transaction rolled back after 30s threshold.",
                Priority = ErrorPriority.Alta,
                Status = ErrorStatus.New,
                Occurrences = 87,
                FirstSeen = now.AddDays(-1),
                LastSeen = now.AddHours(-1),
                ExceptionMessage = "System.TimeoutException: The operation has timed out.",
                StackTrace = "at PaymentGateway.Controllers.PaymentController.ProcessPayment(PaymentRequest req)\r\n   at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.SyncActionResultExecutor.Execute()",
                ActivityLog =
                [
                    new() { Timestamp = now.AddDays(-1), User = "System", Message = "Error detected" }
                ]
            },
            new ErrorItem
            {
                Id = 10,
                Code = "WRK-4400",
                Company = "Mapcel S.A.",
                Program = "Notification Service",
                Module = "PushNotifier.SendBatch",
                Description = "Push notification batch delivery failed. Firebase returned 503 Service Unavailable.",
                Priority = ErrorPriority.Media,
                Status = ErrorStatus.New,
                Occurrences = 34,
                FirstSeen = now.AddHours(-18),
                LastSeen = now.AddHours(-2),
                ExceptionMessage = "HttpRequestException: Response status code does not indicate success: 503 (Service Unavailable).",
                StackTrace = "at NotificationService.PushNotifier.SendBatch(List`1 tokens) in /src/Services/PushNotifier.cs:line 78\r\n   at System.Net.Http.HttpClient.SendAsync(HttpRequestMessage request)",
                Assignee = "Sarah Chen",
                IsSilenced = true,
                Environment = "Producción",
                Channel = "Push Notifications",
                Category = "Integración",
                Process = "Envío de notificaciones push",
                Tags = ["intermitente", "integración"],
                CreatedBy = "System",
                ModifiedBy = "Sarah Chen",
                LastUpdated = now.AddHours(-10),
                LatestComment = "Notificaciones silenciadas - outage conocido de Firebase",
                NotificationContacts =
                [
                    new() { Name = "Sarah Chen", Email = "sarah@mapcel.com", Role = "Developer" },
                    new() { Name = "Carlos López", Email = "carlos@mapcel.com", Role = "Backend Lead" }
                ],
                NotificationFrequencyMinutes = 60,
                SilencedUntil = now.AddHours(12),
                LastNotificationSent = now.AddHours(-3),
                ActivityLog =
                [
                    new() { Timestamp = now.AddHours(-18), User = "System", Message = "Error detectado" },
                    new() { Timestamp = now.AddHours(-10), User = "Sarah Chen", Message = "Notificaciones silenciadas - outage conocido de Firebase" }
                ]
            },
            new ErrorItem
            {
                Id = 11,
                Code = "INT-7010",
                Company = "Logística Express",
                Program = "CRM Integration",
                Module = "CrmSyncService.PushContact",
                Description = "CRM API rejected contact sync with 422 error. Invalid field mapping on custom fields.",
                Priority = ErrorPriority.Alta,
                Status = ErrorStatus.InReview,
                Occurrences = 65,
                FirstSeen = now.AddDays(-4),
                LastSeen = now.AddHours(-3),
                ExceptionMessage = "HttpRequestException: Response status code does not indicate success: 422 (Unprocessable Entity).",
                StackTrace = "at CrmIntegration.Services.CrmSyncService.PushContact(ContactDto contact) in /src/Services/CrmSyncService.cs:line 89\r\n   at System.Net.Http.HttpClient.SendAsync(HttpRequestMessage request)",
                Assignee = "Miguel Ángel Torres",
                ActivityLog =
                [
                    new() { Timestamp = now.AddDays(-4), User = "System", Message = "Error detected" },
                    new() { Timestamp = now.AddDays(-3), User = "Miguel Ángel Torres", Message = "Status changed to InReview" }
                ]
            },
            new ErrorItem
            {
                Id = 12,
                Code = "API-6100",
                Company = "Mapcel S.A.",
                Program = "Inventory Service",
                Module = "StockController.UpdateQuantity",
                Description = "Concurrency conflict updating stock quantity. Optimistic concurrency check failed.",
                Priority = ErrorPriority.Baja,
                Status = ErrorStatus.New,
                Occurrences = 7,
                FirstSeen = now.AddHours(-6),
                LastSeen = now.AddHours(-3),
                ExceptionMessage = "DbUpdateConcurrencyException: The database operation was expected to affect 1 row(s), but actually affected 0 row(s).",
                StackTrace = "at InventoryService.Controllers.StockController.UpdateQuantity(StockUpdateRequest req) in /src/Controllers/StockController.cs:line 42\r\n   at Microsoft.EntityFrameworkCore.Update.AffectedCountModificationCommandBatch.ThrowAggregateUpdateConcurrencyException()",
                ActivityLog =
                [
                    new() { Timestamp = now.AddHours(-6), User = "System", Message = "Error detected" }
                ]
            }
        ];
    }
}
