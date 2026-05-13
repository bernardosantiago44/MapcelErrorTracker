using System.Data;
using System.Globalization;
using System.Text;
using MapcelErrorTracker.Exceptions;
using MapcelErrorTracker.Models;
using Microsoft.Data.SqlClient;

namespace MapcelErrorTracker.Services;

public class ErrorService(
    IConfiguration configuration,
    ILogger<ErrorService> logger)
    : BaseService(configuration, logger), IErrorService
{
    private const string SqlSelectRecentErrors = """
        SELECT e.[err_ID],
               e.[err_CodigoError],
               e.[err_DescripcioError],
               e.[err_Programa_Nombre],
               e.[err_Programa_Modulo],
               e.[err_Programa_Proceso],
               e.[err_Prioridad],
               e.[err_FechaGen],
               e.[err_FechaUlt],
               e.[err_Contador],
               e.[err_IdEnterprise],
               e.[err_Exception_MstLast],
               e.[err_Exception_StackTrace],
               e.[err_ErrorAlEnviar],
               e.[err_MsgBody],
               e.[err_MsgSubject],
               e.[err_Procesado],
               e.[err_UbicacionProgrm],
               e.[err_ComentariosAdic],
               e.[err_NombreModifico],
               e.[err_NumeroFolio],
               e.[err_FirstNotif],
               e.[err_LastNotif],
               e.[err_NumAviso],
               e.[err_MailSended],
               e.[err_Status],
               e.[err_ProgAsignado],
               p.[prog_ID] AS [assigned_prog_ID],
               p.[prog_nombre] AS [assigned_prog_nombre],
               p.[prog_telegram_id] AS [assigned_prog_telegram_id],
               p.[prog_celular] AS [assigned_prog_celular]
        FROM [MapaLocalizadorVisor].[dbo].[ErrorSistema] AS e
        LEFT JOIN [MapaLocalizadorVisor].[dbo].[ErroresProgramadores] AS p
            ON p.[prog_ID] = e.[err_ProgAsignado]
        WHERE e.[err_Procesado] IS NULL
           OR (e.[err_Procesado] IS NOT NULL AND DATEDIFF(DAY, COALESCE(e.[err_FechaUlt], e.[err_FechaGen]), GETDATE()) <= 1);
        """;
    private const string SqlSelectErrorById = """
        SELECT e.[err_ID],
               e.[err_CodigoError],
               e.[err_DescripcioError],
               e.[err_Programa_Nombre],
               e.[err_Programa_Modulo],
               e.[err_Programa_Proceso],
               e.[err_Prioridad],
               e.[err_FechaGen],
               e.[err_FechaUlt],
               e.[err_Contador],
               e.[err_IdEnterprise],
               e.[err_Exception_MstLast],
               e.[err_Exception_StackTrace],
               e.[err_ErrorAlEnviar],
               e.[err_MsgBody],
               e.[err_MsgSubject],
               e.[err_Procesado],
               e.[err_UbicacionProgrm],
               e.[err_ComentariosAdic],
               e.[err_NombreModifico],
               e.[err_NumeroFolio],
               e.[err_FirstNotif],
               e.[err_LastNotif],
               e.[err_NumAviso],
               e.[err_MailSended],
               e.[err_Status],
               e.[err_ProgAsignado],
               p.[prog_ID] AS [assigned_prog_ID],
               p.[prog_nombre] AS [assigned_prog_nombre],
               p.[prog_telegram_id] AS [assigned_prog_telegram_id],
               p.[prog_celular] AS [assigned_prog_celular]
        FROM [MapaLocalizadorVisor].[dbo].[ErrorSistema] AS e
        LEFT JOIN [MapaLocalizadorVisor].[dbo].[ErroresProgramadores] AS p
            ON p.[prog_ID] = e.[err_ProgAsignado]
        WHERE e.[err_ID] = @id;
        """;
    private const string SqlUpdateErrorPriority = """
        UPDATE [MapaLocalizadorVisor].[dbo].[ErrorSistema]
        SET [err_Prioridad] = @priority,
            [err_NombreModifico] = @modifiedBy
        WHERE [err_ID] = @id;
        """;
    private const string SqlUpdateErrorStatus = """
        UPDATE [MapaLocalizadorVisor].[dbo].[ErrorSistema]
        SET [err_Status] = @status,
            [err_NombreModifico] = @modifiedBy
        WHERE [err_ID] = @id;
        """;
    private const string SqlAssignErrorUser = """
        UPDATE [MapaLocalizadorVisor].[dbo].[ErrorSistema]
        SET [err_ProgAsignado] = @assignedUserId,
            [err_Status] = CASE
                WHEN @assignedUserId IS NULL THEN [err_Status]
                ELSE @reviewStatus
            END,
            [err_NombreModifico] = @modifiedBy
        WHERE [err_ID] = @id;
        """;

    public async Task<ErrorListViewModel> GetListAsync(
        ErrorListQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            var errors = await GetRecentErrorsAsync(cancellationToken);
            return BuildListViewModel(errors, query);
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to load errors from the database.");
            throw;
        }
    }

    public async Task<ErrorItem> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(SqlSelectErrorById, connection);
            command.CommandType = CommandType.Text;
            command.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = id });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new NotFoundException(nameof(ErrorItem));
            }

            return MapErrorItem(reader);
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to load error {ErrorId} from the database.", id);
            throw;
        }
    }

    public async Task UpdateStatusAsync(
        long id,
        ErrorStatus status,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ValidateStatus(status);

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(SqlUpdateErrorStatus, connection);
            command.CommandType = CommandType.Text;
            command.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = id });
            command.Parameters.Add(new SqlParameter("@status", SqlDbType.NVarChar, 20)
            {
                Value = ToDatabaseStatus(status)
            });
            command.Parameters.Add(new SqlParameter("@modifiedBy", SqlDbType.VarChar, 25)
            {
                Value = "MapcelErrorTracker"
            });

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

            if (rowsAffected == 0)
            {
                throw new NotFoundException(nameof(ErrorItem));
            }

            logger.LogInformation("Status updated for error {ErrorId}: {Status}.", id, status);
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to update status for error {ErrorId}.", id);
            throw;
        }
    }

    public async Task UpdatePriorityAsync(
        long id,
        ErrorPriority priority,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(SqlUpdateErrorPriority, connection);
            command.CommandType = CommandType.Text;
            command.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = id });
            command.Parameters.Add(new SqlParameter("@priority", SqlDbType.VarChar, 15) { Value = priority.ToString() });
            command.Parameters.Add(new SqlParameter("@modifiedBy", SqlDbType.VarChar, 25) { Value = "MapcelErrorTracker" });

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

            if (rowsAffected == 0)
            {
                throw new NotFoundException(nameof(ErrorItem));
            }

            logger.LogInformation("Priority updated for error {ErrorId}: {Priority}.", id, priority);
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to update priority for error {ErrorId}.", id);
            throw;
        }
    }

    public async Task AssignUserAsync(
        long id,
        int? userId,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        if (userId.HasValue && userId.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), userId, "Assigned user id must be positive.");
        }

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(SqlAssignErrorUser, connection);
            command.CommandType = CommandType.Text;
            command.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = id });
            command.Parameters.Add(new SqlParameter("@assignedUserId", SqlDbType.Int)
            {
                Value = userId.HasValue ? userId.Value : DBNull.Value
            });
            command.Parameters.Add(new SqlParameter("@reviewStatus", SqlDbType.NVarChar, 20)
            {
                Value = ToDatabaseStatus(ErrorStatus.EnRevision)
            });
            command.Parameters.Add(new SqlParameter("@modifiedBy", SqlDbType.VarChar, 25)
            {
                Value = "MapcelErrorTracker"
            });

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);

            if (rowsAffected == 0)
            {
                throw new NotFoundException(nameof(ErrorItem));
            }

            logger.LogInformation("Assigned programmer {UserId} to error {ErrorId}.", userId, id);
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to assign programmer {UserId} to error {ErrorId}.", userId, id);
            throw;
        }
    }

    private async Task<List<ErrorItem>> GetRecentErrorsAsync(CancellationToken cancellationToken)
    {
        var errors = new List<ErrorItem>();

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(SqlSelectRecentErrors, connection);
        command.CommandType = CommandType.Text;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            errors.Add(MapErrorItem(reader));
        }

        return errors;
    }

    private static ErrorListViewModel BuildListViewModel(
        List<ErrorItem> errors,
        ErrorListQuery query)
    {
        ArgumentNullException.ThrowIfNull(errors);
        query.SortBy = NormalizeSortBy(query.SortBy);
        query.SortDirection = query.SafeSortDirection;
        query.Page = query.SafePage;
        query.PageSize = query.SafePageSize;

        var filtered = ApplyFilters(errors, query);
        var sorted = ApplySort(filtered, query.SortBy, query.SortDirection);
        var filteredErrors = sorted.ToList();
        var filteredRecords = filteredErrors.Count;
        var totalPages = filteredRecords == 0
            ? 1
            : (int)Math.Ceiling(filteredRecords / (double)query.PageSize);
        var currentPage = Math.Min(query.Page, totalPages);

        query.Page = currentPage;

        return new ErrorListViewModel
        {
            Query = query,
            Errors = filteredErrors
                .Skip((currentPage - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList(),
            Companies = errors
                .Select(error => error.Company)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .ToList(),
            Programs = errors
                .Select(error => error.Program)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .ToList(),
            TotalRecords = errors.Count,
            FilteredRecords = filteredRecords,
            CurrentPage = currentPage,
            PageSize = query.PageSize
        };
    }

    private static IEnumerable<ErrorItem> ApplyFilters(
        IEnumerable<ErrorItem> errors,
        ErrorListQuery query)
    {
        var filtered = errors;

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            filtered = filtered.Where(error =>
                Contains(error.Code, search) ||
                Contains(error.Company, search) ||
                Contains(error.Program, search) ||
                Contains(error.Module, search) ||
                Contains(error.Description, search));
        }

        if (!string.IsNullOrWhiteSpace(query.Program))
        {
            var program = query.Program.Trim();
            filtered = filtered.Where(error => Contains(error.Program, program));
        }

        if (TryParseStatusName(query.Status, out var status))
        {
            filtered = filtered.Where(error => error.Status == status);
        }
        else
        {
            filtered = filtered.Where(error => error.Status != ErrorStatus.Resuelto);
            query.Status = null;
        }

        if (!string.IsNullOrWhiteSpace(query.Priority) &&
            Enum.TryParse<ErrorPriority>(query.Priority, ignoreCase: true, out var priority))
        {
            filtered = filtered.Where(error => error.Priority == priority);
        }
        else
        {
            query.Priority = null;
        }

        return filtered;
    }

    private static bool Contains(string source, string value)
    {
        return !string.IsNullOrWhiteSpace(source) &&
               source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        if (!string.IsNullOrWhiteSpace(sortBy) &&
            ErrorListSortFields.Allowed.Contains(sortBy))
        {
            return sortBy;
        }

        return ErrorListSortFields.LastSeen;
    }

    private static IEnumerable<ErrorItem> ApplySort(
        IEnumerable<ErrorItem> errors,
        string sortBy,
        string sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        var sorted = sortBy switch
        {
            ErrorListSortFields.Status => descending
                ? errors.OrderByDescending(error => StatusRank(error.Status))
                : errors.OrderBy(error => StatusRank(error.Status)),
            ErrorListSortFields.Priority => descending
                ? errors.OrderByDescending(error => PriorityRank(error.Priority))
                : errors.OrderBy(error => PriorityRank(error.Priority)),
            ErrorListSortFields.Company => descending
                ? errors.OrderByDescending(error => error.Company)
                : errors.OrderBy(error => error.Company),
            ErrorListSortFields.ErrorCode => descending
                ? errors.OrderByDescending(error => error.Code)
                : errors.OrderBy(error => error.Code),
            ErrorListSortFields.Occurrences => descending
                ? errors.OrderByDescending(error => error.Occurrences)
                : errors.OrderBy(error => error.Occurrences),
            ErrorListSortFields.FirstSeen => descending
                ? errors.OrderByDescending(error => error.FirstSeen)
                : errors.OrderBy(error => error.FirstSeen),
            _ => descending
                ? errors.OrderByDescending(error => error.LastSeen)
                : errors.OrderBy(error => error.LastSeen)
        };

        return sorted.ThenByDescending(error => error.LastSeen).ThenBy(error => error.Code);
    }

    private static int PriorityRank(ErrorPriority priority)
    {
        return priority switch
        {
            ErrorPriority.Alta => 3,
            ErrorPriority.Media => 2,
            _ => 1
        };
    }

    private static int StatusRank(ErrorStatus status)
    {
        return status switch
        {
            ErrorStatus.SinAsignar => 0,
            ErrorStatus.Nuevo => 1,
            ErrorStatus.EnRevision => 2,
            ErrorStatus.Pospuesto => 3,
            ErrorStatus.Resuelto => 4,
            _ => 5
        };
    }

    private static bool TryParseStatusName(string? value, out ErrorStatus status)
    {
        status = default;

        return !string.IsNullOrWhiteSpace(value) &&
               Enum.TryParse(value, ignoreCase: true, out status) &&
               Enum.IsDefined(status);
    }

    private ErrorItem MapErrorItem(SqlDataReader reader)
    {
        var firstSeen = GetNullableDateTime(reader, "err_FechaGen") ?? DateTime.UtcNow;
        var lastSeen = GetNullableDateTime(reader, "err_FechaUlt") ?? firstSeen;
        var processedAt = GetNullableDateTime(reader, "err_Procesado");
        var enterpriseId = GetNullableInt32(reader, "err_IdEnterprise");
        var assignedUserId = GetNullableInt32(reader, "err_ProgAsignado");

        return new ErrorItem
        {
            Id = GetRequiredInt64(reader, "err_ID"),
            Code = GetRequiredString(reader, "err_CodigoError"),
            Description = GetRequiredString(reader, "err_DescripcioError"),
            Program = GetRequiredString(reader, "err_Programa_Nombre"),
            Module = GetRequiredString(reader, "err_Programa_Modulo"),
            Process = GetNullableString(reader, "err_Programa_Proceso"),
            Priority = ParsePriority(GetRequiredString(reader, "err_Prioridad")),
            Status = ParseStatus(GetNullableString(reader, "err_Status")),
            Occurrences = GetNullableInt16(reader, "err_Contador") ?? 0,
            FirstSeen = firstSeen,
            LastSeen = lastSeen,
            Company = enterpriseId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            AssignedUserId = assignedUserId,
            AssignedUser = MapAssignedUser(reader),
            ExceptionMessage = GetNullableString(reader, "err_Exception_MstLast"),
            StackTrace = GetNullableString(reader, "err_Exception_StackTrace"),
            RequestPayload = GetNullableString(reader, "err_MsgBody"),
            ResponsePayload = GetNullableString(reader, "err_ErrorAlEnviar"),
            LatestComment = GetNullableString(reader, "err_ComentariosAdic"),
            ModifiedBy = GetNullableString(reader, "err_NombreModifico"),
            FolioNumber = GetNullableString(reader, "err_NumeroFolio"),
            Location = GetNullableString(reader, "err_UbicacionProgrm"),
            LastNotificationSent = GetNullableDateTime(reader, "err_LastNotif"),
            NotificationFrequencyMinutes = 0,
            IsSilenced = false,
            ActivityLog = BuildActivityLog(firstSeen, lastSeen, processedAt)
        };
    }

    private static ProgrammerUser? MapAssignedUser(SqlDataReader reader)
    {
        var assignedUserId = GetNullableInt32(reader, "assigned_prog_ID");

        if (!assignedUserId.HasValue)
        {
            return null;
        }

        return new ProgrammerUser
        {
            Id = assignedUserId.Value,
            Name = GetRequiredString(reader, "assigned_prog_nombre"),
            TelegramId = GetNullableString(reader, "assigned_prog_telegram_id"),
            CellPhone = GetNullableString(reader, "assigned_prog_celular")
        };
    }

    private ErrorPriority ParsePriority(string value)
    {
        if (Enum.TryParse<ErrorPriority>(value, ignoreCase: true, out var priority))
        {
            return priority;
        }

        logger.LogWarning(
            "Unknown priority value {PriorityValue} found in err_Prioridad. Defaulting to Media.",
            value);

        return ErrorPriority.Media;
    }

    private ErrorStatus ParseStatus(string dbStatus)
    {
        var normalizedStatus = NormalizeStatus(dbStatus);
        var status = normalizedStatus switch
        {
            "" => ErrorStatus.SinAsignar,
            "nuevo" => ErrorStatus.Nuevo,
            "en revision" => ErrorStatus.EnRevision,
            "pospuesto" => ErrorStatus.Pospuesto,
            "resuelto" => ErrorStatus.Resuelto,
            "sin asignar" => ErrorStatus.SinAsignar,
            _ => throw new DataException($"Unknown err_Status value '{dbStatus}'.")
        };

        if (string.IsNullOrWhiteSpace(dbStatus))
        {
            logger.LogWarning("Empty err_Status value found. Defaulting to {Status}.", status);
        }

        return status;
    }

    private static string ToDatabaseStatus(ErrorStatus status)
    {
        ValidateStatus(status);

        return status switch
        {
            ErrorStatus.Nuevo => "Nuevo",
            ErrorStatus.EnRevision => "En revisión",
            ErrorStatus.Pospuesto => "Pospuesto",
            ErrorStatus.Resuelto => "Resuelto",
            ErrorStatus.SinAsignar => "Sin asignar",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported error status.")
        };
    }

    private static void ValidateStatus(ErrorStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported error status.");
        }
    }

    private static string NormalizeStatus(string value)
    {
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static List<ActivityLogEntry> BuildActivityLog(
        DateTime firstSeen,
        DateTime lastSeen,
        DateTime? processedAt)
    {
        var activityLog = new List<ActivityLogEntry>
        {
            new()
            {
                Timestamp = firstSeen,
                User = "System",
                Message = "Error detectado"
            }
        };

        if (lastSeen != firstSeen)
        {
            activityLog.Add(new ActivityLogEntry
            {
                Timestamp = lastSeen,
                User = "System",
                Message = "Última ocurrencia registrada"
            });
        }

        if (processedAt.HasValue)
        {
            activityLog.Add(new ActivityLogEntry
            {
                Timestamp = processedAt.Value,
                User = "System",
                Message = "Error marcado como procesado"
            });
        }

        return activityLog;
    }

    private static string GetNullableString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) 
            ? string.Empty 
            : reader.GetString(ordinal);
    }

    private static string GetRequiredString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal) 
            ? throw new DataException($"Required database column {columnName} was null.") 
            : reader.GetString(ordinal);
    }

    private static long GetRequiredInt64(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal) 
            ? throw new DataException($"Required database column {columnName} was null.") 
            : reader.GetInt64(ordinal);
    }

    private static DateTime? GetNullableDateTime(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static int? GetNullableInt32(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static short? GetNullableInt16(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt16(ordinal);
    }
}
