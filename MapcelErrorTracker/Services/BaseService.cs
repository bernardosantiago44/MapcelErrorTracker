using MapcelErrorTracker.Exceptions;
using Serilog;

namespace MapcelErrorTracker.Services;

public abstract class BaseService
{
    private readonly IConfiguration _configuration;
    protected string ConnectionString;
    protected readonly ILogger<BaseService> Logger;

    protected BaseService(IConfiguration configuration, ILogger<BaseService> logger)
    {
        _configuration = configuration;
        Logger = logger;
        ConnectionString = string.Empty;
        SetupConnectionString("DevelopmentConnection");
    }
    
    private void SetupConnectionString(string connectionName)
    {
        var connection = _configuration.GetConnectionString(connectionName);
        if (string.IsNullOrEmpty(connection))
        {
            Log.Fatal("Connection string not found in the appsettings.json");
            throw new Exception($"Connection {connectionName} could not be found in appsettings.json.");
        }
        ConnectionString = connection;
    }
}