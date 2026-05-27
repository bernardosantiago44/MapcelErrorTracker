using System.Data;
using MapcelErrorTracker.Models;
using Microsoft.Data.SqlClient;

namespace MapcelErrorTracker.Services;

public class UsersService(
    IConfiguration configuration,
    IWebHostEnvironment env,
    ILogger<UsersService> logger)
    : BaseService(env, configuration, logger), IUsersService
{
    private const string SqlSelectUsers = """
        SELECT [prog_ID],
               [prog_nombre],
               [prog_telegram_id],
               [prog_celular]
        FROM [MapaLocalizadorVisor].[dbo].[ErroresProgramadores]
        ORDER BY [prog_nombre];
        """;

    public async Task<IReadOnlyList<ProgrammerUser>> GetAllAsync(CancellationToken cancellationToken)
    {
        var users = new List<ProgrammerUser>();

        try
        {
            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(SqlSelectUsers, connection);
            command.CommandType = CommandType.Text;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                users.Add(new ProgrammerUser
                {
                    Id = GetRequiredInt32(reader, "prog_ID"),
                    Name = GetRequiredString(reader, "prog_nombre"),
                    TelegramId = GetNullableString(reader, "prog_telegram_id"),
                    CellPhone = GetNullableString(reader, "prog_celular")
                });
            }

            return users;
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Unable to load programmer users from the database.");
            throw;
        }
    }
}
