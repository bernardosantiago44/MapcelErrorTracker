namespace MapcelErrorTracker.Services;

public abstract class BaseService
{
    private const string DefaultConnectionName = "DevelopmentConnection";
    private const string ConnectionNameConfigurationKey = "Database:ConnectionName";

    protected BaseService(IConfiguration configuration, ILogger logger)
    {
        var connectionName = configuration[ConnectionNameConfigurationKey] ?? DefaultConnectionName;
        ConnectionString = GetRequiredConnectionString(configuration, logger, connectionName);
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
}
