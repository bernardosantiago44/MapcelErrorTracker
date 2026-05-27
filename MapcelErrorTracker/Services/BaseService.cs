using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace MapcelErrorTracker.Services;

public abstract class BaseService
{
    private const string ProductionConnectionConfigurationKey = "DefaultConnection";
    private const string DevelopmentConnectionConfigurationKey = "LocalConnection";

    protected BaseService(IWebHostEnvironment env, IConfiguration configuration, ILogger logger)
    {
        var connectionName = env.IsDevelopment() ? DevelopmentConnectionConfigurationKey : ProductionConnectionConfigurationKey;
        ConnectionString = GetRequiredConnectionString(configuration, logger, connectionName);
        TestDbConnection(logger);
    }

    protected string ConnectionString { get; }

    private static string GetRequiredConnectionString(
        IConfiguration configuration,
        ILogger logger,
        string connectionName)
    {
        var connection = configuration.GetConnectionString(connectionName);

        if (string.IsNullOrEmpty(connection))
        {
            logger.LogCritical("Connection string {ConnectionName} was not configured.", connectionName);
            throw new InvalidOperationException($"Connection {connectionName} could not be found in appsettings.json.");
        }

        return connection;
    }

    private void TestDbConnection(ILogger logger)
    {
        var connection = new SqlConnection(ConnectionString);
        try
        {
            connection.Open();
        }
        catch (SqlException e)
        {
            logger.LogError(e, "Could not connect to database {Error}", e.Message);
        }
        finally
        {
            connection.Close();
        }
    }
    
    // ---------------- Extract data from columns given a reader and column name ----------------
    protected static string GetRequiredString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? throw new DataException($"Required database column {columnName} was null.")
            : reader.GetString(ordinal);
    }
    
    protected static string GetNullableString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal)
            ? string.Empty
            : reader.GetString(ordinal);
    }
    
    protected static int GetRequiredInt32(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? throw new DataException($"Required database column {columnName} was null.")
            : reader.GetInt32(ordinal);
    }
    
    protected static long GetRequiredInt64(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal) 
            ? throw new DataException($"Required database column {columnName} was null.") 
            : reader.GetInt64(ordinal);
    }
    
    protected static DateTime GetRequiredDateTime(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? throw new DataException($"Required database column {columnName} was null.")
            : reader.GetDateTime(ordinal);
    }
    
    protected static double GetRequiredDouble(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal)
            ? throw new DataException($"Required database column {columnName} was null.")
            : Convert.ToDouble(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    protected static DateTime? GetNullableDateTime(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    protected static int? GetNullableInt32(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    protected static short? GetNullableInt16(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt16(ordinal);
    }
}
