using System.Data;
using MapcelErrorTracker.Exceptions;
using MapcelErrorTracker.Models;
using Microsoft.Data.SqlClient;

namespace MapcelErrorTracker.Services;

public interface IErrorService
{
    /// <summary>
    /// Returns the ErrorItem matching the given id from the database, if it exists.
    /// 
    /// Looks for the item where the column `err_ID` = id.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="token"></param>
    /// <returns>The error item, if found</returns>
    Task<Dictionary<string, string>> FindByIdAsync(long id, CancellationToken token);
}

public class ErrorService(IConfiguration configuration, ILogger<ErrorService> logger) : BaseService(configuration, logger), IErrorService
{
    private const string SqlSelectAllErrorItems = """
                        SELECT [err_ID]
                                ,[err_CodigoError]
                                ,[err_DescripcioError]
                                ,[err_Programa_Nombre]
                                ,[err_Programa_Modulo]
                                ,[err_Programa_Proceso]
                                ,[err_ReferenceType]
                                ,[err_ReferenceID]
                                ,[err_Prioridad]
                                ,[err_FechaGen]
                                ,[err_FechaUlt]
                                ,[err_Contador]
                                ,[err_ContadorNumMax]
                                ,[err_IdEnterprise]
                                ,[ENTERPRISE_NAME]
                                ,[err_Exception_MstLast]
                                ,[err_Exception_StackTrace]
                                ,[err_ErrorAlEnviar]
                                ,[err_MsgBody]
                                ,[err_MsgSubject]
                                ,[err_Procesado]
                                ,[err_UbicacionProgrm]
                                ,[err_ComentariosAdic]
                                ,[err_NombreModifico]
                                ,[err_NumeroFolio]
                                ,[err_FirstNotif]
                                ,[err_LastNotif]
                                ,[err_NumAviso]
                               FROM [MapaLocalizadorVisor].[dbo].[ErrorSistema]
                             LEFT JOIN [MapaLocalizadorVisor].[dbo].[MNG_ENTERPRISES] ON [ENTERPRISE_ID] = [err_IdEnterprise]
                              WHERE [err_Procesado] IS NULL 
                               OR ( [err_Procesado] IS NOT NULL AND DATEDIFF(DAY, [err_FechaUlt], GETDATE()) <= 1 )
                            ORDER BY
                             IIF([err_Procesado] IS NULL, 0, 1),       -- primeros los NULL
                             CASE WHEN [err_Procesado] IS NULL THEN [err_FechaGen] END ,  -- dentro de NULL: por err_FechaGen asc
                             [err_Procesado] DESC  -- después: por err_Procesado desc
                    """;

    private const string SqlSelectByErrorId = """
        SELECT [err_ID], [err_CodigoError], [err_DescripcioError]
        FROM [MapaLocalizadorVisor].[dbo].[ErrorSistema]
        WHERE [err_ID] = @id;
    """;
    
    public async Task<Dictionary<string, string>> FindByIdAsync(long id, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(token);

        await using var selectCmd = new SqlCommand(SqlSelectByErrorId, connection);
        selectCmd.CommandType = CommandType.Text;
        selectCmd.Parameters.Add(new SqlParameter("@id", SqlDbType.BigInt) { Value = id });
        
        await using var reader = await selectCmd.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token).ConfigureAwait(false)) throw new NotFoundException("ErrorItem");
        var errorId = reader.GetInt64(0);
        var codigoError = reader.GetString(1);
        var description = reader.GetString(2);
            
        return new Dictionary<string, string>() 
        {
            { "id", errorId.ToString() },
            { "codigoError", codigoError },
            { "description", description }
        };

    }
}