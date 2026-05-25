using System.Data;
using System.Globalization;
using System.Text;
using MapcelErrorTracker.Exceptions;
using MapcelErrorTracker.Models;
using Microsoft.Data.SqlClient;

namespace MapcelErrorTracker.Services;

public class ErrorService(
    IConfiguration configuration,
    IWebHostEnvironment env,
    ILogger<ErrorService> logger)
    : BaseService(env, configuration, logger), IErrorService
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
               e.[err_Status]
        FROM [MapaLocalizadorVisor].[dbo].[ErrorSistema] AS e
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
               e.[err_Status]
        FROM [MapaLocalizadorVisor].[dbo].[ErrorSistema] AS e
        WHERE e.[err_ID] = @id;
        """;
    private const string SqlSelectAssignedUsersByErrorId = """
        SELECT p.[prog_ID],
               p.[prog_nombre],
               p.[prog_telegram_id],
               p.[prog_celular]
        FROM [MapaLocalizadorVisor].[dbo].[ErroresAsignaturas] AS a
        INNER JOIN [MapaLocalizadorVisor].[dbo].[ErroresProgramadores] AS p
            ON p.[prog_ID] = a.[programadorId]
        WHERE a.[errorId] = @id
        ORDER BY p.[prog_nombre];
        """;
    private const string SqlSelectAssignedUsersForErrors = """
        SELECT a.[errorId],
               p.[prog_ID],
               p.[prog_nombre],
               p.[prog_telegram_id],
               p.[prog_celular]
        FROM [MapaLocalizadorVisor].[dbo].[ErroresAsignaturas] AS a
        INNER JOIN [MapaLocalizadorVisor].[dbo].[ErroresProgramadores] AS p
            ON p.[prog_ID] = a.[programadorId]
        WHERE a.[errorId] IN ({0})
        ORDER BY a.[errorId], p.[prog_nombre];
        """;
    private const string SqlSelectOccurrenceMetricsForErrors = """
        SELECT h.[errh_err_ID] AS [ErrorId],
               MIN(h.[errh_Created]) AS [FirstOccurrenceAt],
               MAX(h.[errh_Created]) AS [LastOccurrenceAt],
               COUNT_BIG(*) AS [TotalOccurrences],
               SUM(CASE WHEN h.[errh_Created] >= @oneHourAgo THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS [OccurrencesLast1H],
               SUM(CASE WHEN h.[errh_Created] >= @twentyFourHoursAgo THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS [OccurrencesLast24H],
               SUM(CASE WHEN h.[errh_Created] >= @sevenDaysAgo THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS [OccurrencesLast7D]
        FROM [MapaLocalizadorVisor].[dbo].[ErrorSistemaHistory] AS h
        WHERE h.[errh_err_ID] IN ({0})
        GROUP BY h.[errh_err_ID];
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
    private const string SqlDeleteAssignedUsers = """
        DELETE FROM [MapaLocalizadorVisor].[dbo].[ErroresAsignaturas]
        WHERE [errorId] = @id;
        """;
    private const string SqlInsertAssignedUser = """
        INSERT INTO [MapaLocalizadorVisor].[dbo].[ErroresAsignaturas]
            ([errorId], [programadorId])
        VALUES
            (@id, @userId);
        """;
    private const string SqlUpdateErrorAssignmentState = """
        UPDATE [MapaLocalizadorVisor].[dbo].[ErrorSistema]
        SET [err_Status] = CASE
                WHEN @hasAssignedUsers = 1 THEN @reviewStatus
                ELSE [err_Status]
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
            await PopulateHeatScoresAsync(errors, cancellationToken);
            var model = BuildListViewModel(errors, query);
            await PopulateAssignedUsersAsync(model.Errors, cancellationToken);

            return model;
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

            ErrorItem error;
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new NotFoundException(nameof(ErrorItem));
                }

                error = MapErrorItem(reader);
            }

            error.AssignedUsers = await GetAssignedUsersAsync(connection, id, cancellationToken);

            return error;
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

    public async Task AssignUsersAsync(
        long id,
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentNullException.ThrowIfNull(userIds);

        if (userIds.Any(userId => userId <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(userIds), userIds, "Assigned user ids must be positive.");
        }

        var distinctUserIds = userIds
            .Distinct()
            .OrderBy(userId => userId)
            .ToList();

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                await using (var updateCommand = new SqlCommand(SqlUpdateErrorAssignmentState, connection, transaction))
                {
                    updateCommand.CommandType = CommandType.Text;
                    updateCommand.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = id });
                    updateCommand.Parameters.Add(new SqlParameter("@hasAssignedUsers", SqlDbType.Bit)
                    {
                        Value = distinctUserIds.Count > 0
                    });
                    updateCommand.Parameters.Add(new SqlParameter("@reviewStatus", SqlDbType.NVarChar, 20)
                    {
                        Value = ToDatabaseStatus(ErrorStatus.EnRevision)
                    });
                    updateCommand.Parameters.Add(new SqlParameter("@modifiedBy", SqlDbType.VarChar, 25)
                    {
                        Value = "MapcelErrorTracker"
                    });

                    var rowsAffected = await updateCommand.ExecuteNonQueryAsync(cancellationToken);

                    if (rowsAffected == 0)
                    {
                        throw new NotFoundException(nameof(ErrorItem));
                    }
                }

                await using (var deleteCommand = new SqlCommand(SqlDeleteAssignedUsers, connection, transaction))
                {
                    deleteCommand.CommandType = CommandType.Text;
                    deleteCommand.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = id });
                    await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                foreach (var userId in distinctUserIds)
                {
                    await using var insertCommand = new SqlCommand(SqlInsertAssignedUser, connection, transaction);
                    insertCommand.CommandType = CommandType.Text;
                    insertCommand.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = id });
                    insertCommand.Parameters.Add(new SqlParameter("@userId", SqlDbType.Int) { Value = userId });
                    await insertCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }

            logger.LogInformation("Assigned {UserCount} programmers to error {ErrorId}.", distinctUserIds.Count, id);
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to assign programmers to error {ErrorId}.", id);
            throw;
        }
    }

    private static async Task<List<ProgrammerUser>> GetAssignedUsersAsync(
        SqlConnection connection,
        long id,
        CancellationToken cancellationToken)
    {
        var users = new List<ProgrammerUser>();

        await using var command = new SqlCommand(SqlSelectAssignedUsersByErrorId, connection);
        command.CommandType = CommandType.Text;
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = id });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(MapProgrammerUser(reader));
        }

        return users;
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

    private async Task PopulateAssignedUsersAsync(
        IReadOnlyList<ErrorItem> errors,
        CancellationToken cancellationToken)
    {
        if (errors.Count == 0)
        {
            return;
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await PopulateAssignedUsersAsync(connection, errors, cancellationToken);
    }

    private async Task PopulateHeatScoresAsync(
        List<ErrorItem> errors,
        CancellationToken cancellationToken)
    {
        if (errors.Count == 0)
        {
            return;
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var chunk in errors.Chunk(500))
        {
            await PopulateHeatScoresAsync(connection, chunk, cancellationToken);
        }
    }

    private static async Task PopulateHeatScoresAsync(
        SqlConnection connection,
        IReadOnlyList<ErrorItem> errors,
        CancellationToken cancellationToken)
    {
        var parameters = errors
            .Select((error, index) => new
            {
                Name = $"@id{index}",
                error.Id
            })
            .ToList();
        var sql = string.Format(
            CultureInfo.InvariantCulture,
            SqlSelectOccurrenceMetricsForErrors,
            string.Join(", ", parameters.Select(parameter => parameter.Name)));
        var now = DateTime.Now;
        var errorsById = errors.ToDictionary(error => error.Id);

        await using var command = new SqlCommand(sql, connection);
        command.CommandType = CommandType.Text;
        command.Parameters.Add(new SqlParameter("@oneHourAgo", SqlDbType.DateTime) { Value = now.AddHours(-1) });
        command.Parameters.Add(new SqlParameter("@twentyFourHoursAgo", SqlDbType.DateTime) { Value = now.AddHours(-24) });
        command.Parameters.Add(new SqlParameter("@sevenDaysAgo", SqlDbType.DateTime) { Value = now.AddDays(-7) });

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new SqlParameter(parameter.Name, SqlDbType.BigInt) { Value = parameter.Id });
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var errorId = GetRequiredInt64(reader, "ErrorId");

            if (!errorsById.TryGetValue(errorId, out var error))
            {
                continue;
            }

            error.HeatScore = ErrorHeatClassifier.Classify(
                new ErrorHeatClassifierInput
                {
                    FirstSeenAt = GetRequiredDateTime(reader, "FirstOccurrenceAt"),
                    LastSeenAt = GetRequiredDateTime(reader, "LastOccurrenceAt"),
                    TotalOccurrences = GetRequiredInt64(reader, "TotalOccurrences"),
                    OccurrencesLast1H = GetRequiredInt64(reader, "OccurrencesLast1H"),
                    OccurrencesLast24H = GetRequiredInt64(reader, "OccurrencesLast24H"),
                    OccurrencesLast7D = GetRequiredInt64(reader, "OccurrencesLast7D")
                },
                now).HeatScore;
        }
    }

    private static async Task PopulateAssignedUsersAsync(
        SqlConnection connection,
        IReadOnlyList<ErrorItem> errors,
        CancellationToken cancellationToken)
    {
        if (errors.Count == 0)
        {
            return;
        }

        var parameters = errors
            .Select((error, index) => new
            {
                Name = $"@id{index}",
                error.Id
            })
            .ToList();
        var sql = string.Format(
            CultureInfo.InvariantCulture,
            SqlSelectAssignedUsersForErrors,
            string.Join(", ", parameters.Select(parameter => parameter.Name)));
        var errorsById = errors.ToDictionary(error => error.Id);

        await using var command = new SqlCommand(sql, connection);
        command.CommandType = CommandType.Text;

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new SqlParameter(parameter.Name, SqlDbType.BigInt) { Value = parameter.Id });
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var errorId = GetRequiredInt64(reader, "errorId");

            if (errorsById.TryGetValue(errorId, out var error))
            {
                error.AssignedUsers.Add(MapProgrammerUser(reader));
            }
        }
    }

    private static ErrorListViewModel BuildListViewModel(
        List<ErrorItem> errors,
        ErrorListQuery query)
    {
        ArgumentNullException.ThrowIfNull(errors);
        query.SortBy = NormalizeSortBy(query.SortBy);
        query.SortDirection = query.SafeSortDirection;
        if (string.Equals(query.SortBy, ErrorListSortFields.Importance, StringComparison.OrdinalIgnoreCase))
        {
            query.SortDirection = "desc";
        }

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
            ErrorListSortFields.Importance => descending
                ? errors.OrderByDescending(error => error.HeatScore)
                : errors.OrderBy(error => error.HeatScore),
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

    private static ProgrammerUser MapProgrammerUser(SqlDataReader reader)
    {
        return new ProgrammerUser
        {
            Id = GetRequiredInt32(reader, "prog_ID"),
            Name = GetRequiredString(reader, "prog_nombre"),
            TelegramId = GetNullableString(reader, "prog_telegram_id"),
            CellPhone = GetNullableString(reader, "prog_celular")
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

    private static int GetRequiredInt32(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? throw new DataException($"Required database column {columnName} was null.")
            : reader.GetInt32(ordinal);
    }

    private static DateTime GetRequiredDateTime(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? throw new DataException($"Required database column {columnName} was null.")
            : reader.GetDateTime(ordinal);
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
