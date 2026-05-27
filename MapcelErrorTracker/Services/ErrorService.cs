using System.Data;
using System.Globalization;
using System.Text;
using MapcelErrorTracker.Exceptions;
using MapcelErrorTracker.Models;
using MapcelErrorTracker.Models.ErrorQuery;
using Microsoft.Data.SqlClient;

namespace MapcelErrorTracker.Services;

public class ErrorService(
    IConfiguration configuration,
    IWebHostEnvironment env,
    ILogger<ErrorService> logger)
    : BaseService(env, configuration, logger), IErrorService
{
    private const string SqlRecentErrorPredicate = """
        (e.[err_Procesado] IS NULL
            OR e.[err_FechaUlt] >= @recentCutoff
            OR (e.[err_FechaUlt] IS NULL AND e.[err_FechaGen] >= @recentCutoff))
    """;
    private const string SqlStatusRankExpression = """
        CASE
            WHEN e.[err_Status] IS NULL OR LTRIM(RTRIM(e.[err_Status])) = '' THEN 0
            WHEN LOWER(LTRIM(RTRIM(e.[err_Status]))) = N'sin asignar' THEN 0
            WHEN LOWER(LTRIM(RTRIM(e.[err_Status]))) = N'nuevo' THEN 1
            WHEN LOWER(LTRIM(RTRIM(e.[err_Status]))) IN (N'en revisión', N'en revision') THEN 2
            WHEN LOWER(LTRIM(RTRIM(e.[err_Status]))) = N'pospuesto' THEN 3
            WHEN LOWER(LTRIM(RTRIM(e.[err_Status]))) = N'resuelto' THEN 4
            ELSE 0
        END
    """;
    private const string SqlPriorityRankExpression = """
        CASE LOWER(LTRIM(RTRIM(e.[err_Prioridad])))
            WHEN N'alta' THEN 3
            WHEN N'media' THEN 2
            WHEN N'baja' THEN 1
            ELSE 2
        END
    """;
    private const string SqlHeatScoreExpression = """
        CASE
            WHEN occurrenceMetrics.[TotalOccurrences] IS NULL OR occurrenceMetrics.[TotalOccurrences] = 0 THEN CONVERT(float, 0)
            ELSE ROUND(
                occurrenceMetrics.[OccurrencesLast1H] * 3.0 +
                occurrenceMetrics.[OccurrencesLast24H] +
                occurrenceMetrics.[OccurrencesLast7D] * 0.25 +
                CASE
                    WHEN DATEDIFF(MINUTE, occurrenceMetrics.[LastOccurrenceAt], @now) <= 15 THEN 30
                    WHEN DATEDIFF(MINUTE, occurrenceMetrics.[LastOccurrenceAt], @now) <= 60 THEN 20
                    WHEN DATEDIFF(MINUTE, occurrenceMetrics.[LastOccurrenceAt], @now) <= 360 THEN 10
                    ELSE 0
                END +
                CASE
                    WHEN DATEDIFF(HOUR, occurrenceMetrics.[FirstOccurrenceAt], @now) > 720
                         AND occurrenceMetrics.[TotalOccurrences] < 20 THEN -35
                    WHEN DATEDIFF(HOUR, occurrenceMetrics.[FirstOccurrenceAt], @now) > 168
                         AND occurrenceMetrics.[TotalOccurrences] < 10 THEN -20
                    ELSE 0
                END,
                2)
        END
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
            var normalizedQuery = NormalizeListQuery(query);
            var page = await GetErrorListPageAsync(normalizedQuery, cancellationToken);

            if (!string.Equals(normalizedQuery.SortBy, ErrorListSortFields.Importance, StringComparison.Ordinal))
            {
                await PopulateHeatScoresAsync(page.Errors, cancellationToken);
            }

            await PopulateAssignedUsersAsync(page.Errors, cancellationToken);

            query.Page = page.CurrentPage;
            var model = new ErrorListViewModel
            {
                Query = query,
                Errors = page.Errors,
                Programs = page.Programs,
                TotalRecords = page.TotalRecords,
                FilteredRecords = page.FilteredRecords,
                CurrentPage = page.CurrentPage,
                PageSize = normalizedQuery.PageSize
            };

            return model;
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to load errors from the database.");
            throw;
        }
    }

    private sealed record ErrorListPage(
        List<ErrorItem> Errors,
        IReadOnlyList<string> Programs,
        int TotalRecords,
        int FilteredRecords,
        int CurrentPage);

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

    private async Task<ErrorListPage> GetErrorListPageAsync(
        NormalizedErrorListQuery query,
        CancellationToken cancellationToken)
    {
        var errors = new List<ErrorItem>();
        var programs = new List<string>();
        var now = DateTime.Now;

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(BuildListPageSql(query), connection);
        command.CommandType = CommandType.Text;
        AddListPageParameters(command, query, now);

        var totalRecords = 0;
        var filteredRecords = 0;
        var currentPage = 1;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            totalRecords = ToNonNegativeInt(GetRequiredInt64(reader, "TotalRecords"));
            filteredRecords = ToNonNegativeInt(GetRequiredInt64(reader, "FilteredRecords"));
            currentPage = GetRequiredInt32(reader, "CurrentPage");
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            errors.Add(MapErrorListItem(reader));
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            programs.Add(GetRequiredString(reader, "Program"));
        }

        return new ErrorListPage(
            errors,
            programs,
            totalRecords,
            filteredRecords,
            currentPage);
    }

    private static NormalizedErrorListQuery NormalizeListQuery(ErrorListQuery query)
    {
        query.SortBy = NormalizeSortBy(query.SortBy);
        query.SortDirection = query.SafeSortDirection;
        if (string.Equals(query.SortBy, ErrorListSortFields.Importance, StringComparison.Ordinal))
        {
            query.SortDirection = "desc";
        }

        query.Page = query.SafePage;
        query.PageSize = query.SafePageSize;

        var includeAllStatuses = string.Equals(
            query.Status,
            ErrorListQuery.AllStatusesValue,
            StringComparison.OrdinalIgnoreCase);
        ErrorStatus? status = null;

        if (includeAllStatuses)
        {
            query.Status = ErrorListQuery.AllStatusesValue;
        }
        else if (TryParseStatusName(query.Status, out var parsedStatus))
        {
            status = parsedStatus;
            query.Status = parsedStatus.ToString();
        }
        else
        {
            query.Status = null;
        }

        ErrorPriority? priority = null;
        if (!string.IsNullOrWhiteSpace(query.Priority) &&
            Enum.TryParse<ErrorPriority>(query.Priority, ignoreCase: true, out var parsedPriority) &&
            Enum.IsDefined(parsedPriority))
        {
            priority = parsedPriority;
            query.Priority = parsedPriority.ToString();
        }
        else
        {
            query.Priority = null;
        }

        return new NormalizedErrorListQuery(
            NullIfWhiteSpace(query.Search),
            NullIfWhiteSpace(query.Program),
            status,
            includeAllStatuses,
            priority,
            query.SortBy,
            query.SortDirection,
            query.Page,
            query.PageSize);
    }

    private static string BuildListPageSql(NormalizedErrorListQuery query)
    {
        var filteredPredicate = BuildFilteredPredicate(query);
        var heatScoreExpression = string.Equals(query.SortBy, ErrorListSortFields.Importance, StringComparison.Ordinal)
            ? SqlHeatScoreExpression
            : "CONVERT(float, 0)";

        return $$"""
            DECLARE @totalRecords bigint;
            DECLARE @filteredRecords bigint;

            SELECT @totalRecords = COUNT_BIG(*)
            FROM [MapaLocalizadorVisor].[dbo].[ErrorSistema] AS e
            WHERE {{SqlRecentErrorPredicate}};

            SELECT @filteredRecords = COUNT_BIG(*)
            FROM [MapaLocalizadorVisor].[dbo].[ErrorSistema] AS e
            WHERE {{filteredPredicate}};

            DECLARE @totalPages int = CASE
                WHEN @filteredRecords = 0 THEN 1
                ELSE CONVERT(int, CEILING(CONVERT(float, @filteredRecords) / @pageSize))
            END;
            DECLARE @currentPage int = CASE
                WHEN @page > @totalPages THEN @totalPages
                ELSE @page
            END;
            DECLARE @offset int = (@currentPage - 1) * @pageSize;

            SELECT @totalRecords AS [TotalRecords],
                   @filteredRecords AS [FilteredRecords],
                   @currentPage AS [CurrentPage];

            WITH FilteredErrors AS (
                SELECT e.[err_ID],
                       e.[err_CodigoError],
                       e.[err_DescripcioError],
                       e.[err_Programa_Nombre],
                       e.[err_Programa_Modulo],
                       e.[err_Prioridad],
                       e.[err_FechaGen],
                       e.[err_FechaUlt],
                       e.[err_Contador],
                       e.[err_IdEnterprise],
                       e.[err_Status],
                       e.[err_FechaGen] AS [FirstSeen],
                       COALESCE(e.[err_FechaUlt], e.[err_FechaGen]) AS [LastSeen],
                       COALESCE(e.[err_Contador], 0) AS [Occurrences],
                       {{SqlStatusRankExpression}} AS [StatusRank],
                       {{SqlPriorityRankExpression}} AS [PriorityRank],
                       {{heatScoreExpression}} AS [HeatScore]
                FROM [MapaLocalizadorVisor].[dbo].[ErrorSistema] AS e
                {{BuildHeatMetricsApply(query)}}
                WHERE {{filteredPredicate}}
            )
            SELECT [err_ID],
                   [err_CodigoError],
                   [err_DescripcioError],
                   [err_Programa_Nombre],
                   [err_Programa_Modulo],
                   [err_Prioridad],
                   [err_FechaGen],
                   [err_FechaUlt],
                   [err_Contador],
                   [err_IdEnterprise],
                   [err_Status],
                   [HeatScore]
            FROM FilteredErrors
            {{BuildOrderByClause(query)}}
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;

            SELECT [Program]
            FROM (
                SELECT DISTINCT e.[err_Programa_Nombre] AS [Program]
                FROM [MapaLocalizadorVisor].[dbo].[ErrorSistema] AS e
                WHERE {{SqlRecentErrorPredicate}}
                  AND e.[err_Programa_Nombre] IS NOT NULL
                  AND LTRIM(RTRIM(e.[err_Programa_Nombre])) <> ''
            ) AS Programs
            ORDER BY [Program];
            """;
    }

    private static string BuildFilteredPredicate(NormalizedErrorListQuery query)
    {
        var predicates = new List<string> { SqlRecentErrorPredicate };

        if (!query.IncludeAllStatuses)
        {
            predicates.Add(query.Status.HasValue
                ? $"({SqlStatusRankExpression}) = @statusRank"
                : $"({SqlStatusRankExpression}) <> @resolvedStatusRank");
        }

        if (query.Priority.HasValue)
        {
            predicates.Add($"({SqlPriorityRankExpression}) = @priorityRank");
        }

        if (query.Search is not null)
        {
            predicates.Add("""
                (e.[err_CodigoError] LIKE @search ESCAPE '\'
                    OR CONVERT(varchar(20), e.[err_IdEnterprise]) LIKE @search ESCAPE '\'
                    OR e.[err_Programa_Nombre] LIKE @search ESCAPE '\'
                    OR e.[err_Programa_Modulo] LIKE @search ESCAPE '\'
                    OR e.[err_DescripcioError] LIKE @search ESCAPE '\')
                """);
        }

        if (query.Program is not null)
        {
            predicates.Add("e.[err_Programa_Nombre] LIKE @program ESCAPE '\\'");
        }

        return string.Join($"{Environment.NewLine}              AND ", predicates);
    }

    private static string BuildHeatMetricsApply(NormalizedErrorListQuery query)
    {
        return 
            !string.Equals(query.SortBy, ErrorListSortFields.Importance, StringComparison.Ordinal) 
                ? string.Empty 
                : """
                    OUTER APPLY (
                        SELECT MIN(h.[errh_Created]) AS [FirstOccurrenceAt],
                               MAX(h.[errh_Created]) AS [LastOccurrenceAt],
                               COUNT_BIG(*) AS [TotalOccurrences],
                               SUM(CASE WHEN h.[errh_Created] >= @oneHourAgo THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS [OccurrencesLast1H],
                               SUM(CASE WHEN h.[errh_Created] >= @twentyFourHoursAgo THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS [OccurrencesLast24H],
                               SUM(CASE WHEN h.[errh_Created] >= @sevenDaysAgo THEN CONVERT(bigint, 1) ELSE CONVERT(bigint, 0) END) AS [OccurrencesLast7D]
                        FROM [MapaLocalizadorVisor].[dbo].[ErrorSistemaHistory] AS h
                        WHERE h.[errh_err_ID] = e.[err_ID]
                    ) AS occurrenceMetrics
                """;
    }

    private static string BuildOrderByClause(NormalizedErrorListQuery query)
    {
        var direction = string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase)
            ? "ASC"
            : "DESC";
        var orderByColumns = query.SortBy switch
        {
            ErrorListSortFields.Status => new[] { $"[StatusRank] {direction}", "[LastSeen] DESC", "[err_CodigoError] ASC" },
            ErrorListSortFields.Priority => new[] { $"[PriorityRank] {direction}", "[LastSeen] DESC", "[err_CodigoError] ASC" },
            ErrorListSortFields.Company => new[] { $"[err_IdEnterprise] {direction}", "[LastSeen] DESC", "[err_CodigoError] ASC" },
            ErrorListSortFields.ErrorCode => new[] { $"[err_CodigoError] {direction}", "[LastSeen] DESC" },
            ErrorListSortFields.Occurrences => new[] { $"[Occurrences] {direction}", "[LastSeen] DESC", "[err_CodigoError] ASC" },
            ErrorListSortFields.Importance => new[] { "[HeatScore] DESC", "[LastSeen] DESC", "[err_CodigoError] ASC" },
            ErrorListSortFields.FirstSeen => new[] { $"[FirstSeen] {direction}", "[LastSeen] DESC", "[err_CodigoError] ASC" },
            _ => new[] { $"[LastSeen] {direction}", "[err_CodigoError] ASC" }
        };

        return $"ORDER BY {string.Join(", ", orderByColumns)}";
    }

    private static void AddListPageParameters(
        SqlCommand command,
        NormalizedErrorListQuery query,
        DateTime now)
    {
        command.Parameters.Add(new SqlParameter("@recentCutoff", SqlDbType.DateTime) { Value = now.AddDays(-1) });
        command.Parameters.Add(new SqlParameter("@page", SqlDbType.Int) { Value = query.Page });
        command.Parameters.Add(new SqlParameter("@pageSize", SqlDbType.Int) { Value = query.PageSize });
        command.Parameters.Add(new SqlParameter("@resolvedStatusRank", SqlDbType.Int)
        {
            Value = StatusRank(ErrorStatus.Resuelto)
        });
        command.Parameters.Add(new SqlParameter("@now", SqlDbType.DateTime) { Value = now });
        command.Parameters.Add(new SqlParameter("@oneHourAgo", SqlDbType.DateTime) { Value = now.AddHours(-1) });
        command.Parameters.Add(new SqlParameter("@twentyFourHoursAgo", SqlDbType.DateTime) { Value = now.AddHours(-24) });
        command.Parameters.Add(new SqlParameter("@sevenDaysAgo", SqlDbType.DateTime) { Value = now.AddDays(-7) });

        if (query.Status.HasValue)
        {
            command.Parameters.Add(new SqlParameter("@statusRank", SqlDbType.Int)
            {
                Value = StatusRank(query.Status.Value)
            });
        }

        if (query.Priority.HasValue)
        {
            command.Parameters.Add(new SqlParameter("@priorityRank", SqlDbType.Int)
            {
                Value = PriorityRank(query.Priority.Value)
            });
        }

        if (query.Search is not null)
        {
            command.Parameters.Add(new SqlParameter("@search", SqlDbType.NVarChar, 4000)
            {
                Value = ToLikePattern(query.Search)
            });
        }

        if (query.Program is not null)
        {
            command.Parameters.Add(new SqlParameter("@program", SqlDbType.NVarChar, 4000)
            {
                Value = ToLikePattern(query.Program)
            });
        }
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

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ToLikePattern(string value)
    {
        return $"%{EscapeLikeValue(value)}%";
    }

    private static string EscapeLikeValue(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (character is '\\' or '%' or '_' or '[' or ']')
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        return ErrorListSortFields.Allowed.FirstOrDefault(
            field => string.Equals(field, sortBy, StringComparison.OrdinalIgnoreCase)) ?? ErrorListSortFields.LastSeen;
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

    private ErrorItem MapErrorListItem(SqlDataReader reader)
    {
        var firstSeen = GetNullableDateTime(reader, "err_FechaGen") ?? DateTime.UtcNow;
        var lastSeen = GetNullableDateTime(reader, "err_FechaUlt") ?? firstSeen;
        var enterpriseId = GetNullableInt32(reader, "err_IdEnterprise");

        return new ErrorItem
        {
            Id = GetRequiredInt64(reader, "err_ID"),
            Code = GetRequiredString(reader, "err_CodigoError"),
            Description = GetRequiredString(reader, "err_DescripcioError"),
            Program = GetRequiredString(reader, "err_Programa_Nombre"),
            Module = GetRequiredString(reader, "err_Programa_Modulo"),
            Priority = ParsePriority(GetRequiredString(reader, "err_Prioridad")),
            Status = ParseStatus(GetNullableString(reader, "err_Status")),
            Occurrences = GetNullableInt16(reader, "err_Contador") ?? 0,
            HeatScore = GetRequiredDouble(reader, "HeatScore"),
            FirstSeen = firstSeen,
            LastSeen = lastSeen,
            Company = enterpriseId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            ActivityLog = BuildActivityLog(firstSeen, lastSeen, null)
        };
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
        if (Enum.TryParse<ErrorPriority>(value, ignoreCase: true, out var priority) &&
            Enum.IsDefined(priority))
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
            _ => ErrorStatus.SinAsignar
        };

        if (string.IsNullOrWhiteSpace(dbStatus))
        {
            logger.LogWarning("Empty err_Status value found. Defaulting to {Status}.", status);
        }
        else if (status == ErrorStatus.SinAsignar && normalizedStatus != "sin asignar")
        {
            logger.LogWarning(
                "Unknown err_Status value {StatusValue} found. Defaulting to {Status}.",
                dbStatus,
                status);
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

    private static int ToNonNegativeInt(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        return value > int.MaxValue ? int.MaxValue : (int)value;
    }
}
