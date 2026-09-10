using System;
using System.Threading.Tasks;

namespace MoneyFlow.Services;

public class DatabaseConnectionResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ServerInstance { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}

public interface IDatabaseSetupService
{
    Task<DatabaseConnectionResult> TestConnectionAsync(string connectionString);
    Task<DatabaseConnectionResult> InitializeDatabaseAsync();
    string GetActiveConnectionString();
    void SetActiveConnectionString(string connectionString);
}
