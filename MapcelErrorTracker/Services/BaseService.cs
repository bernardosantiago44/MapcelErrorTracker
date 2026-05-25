namespace MapcelErrorTracker.Services;

public abstract class BaseService
{
    private const string ProductionConnectionConfigurationKey = "DefaultConnection";
    private const string DevelopmentConnectionConfigurationKey = "LocalConnection";

    protected BaseService(IWebHostEnvironment env, IConfiguration configuration, ILogger logger)
    {
        var connectionName = env.IsDevelopment() ? DevelopmentConnectionConfigurationKey : ProductionConnectionConfigurationKey;
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
