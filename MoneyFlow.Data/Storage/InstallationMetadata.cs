using System;

namespace MoneyFlow.Data.Storage;

/// <summary>
/// Versioned installation metadata stored in System\installation.dat.
/// Tracks installation state, configuration version, unique installation ID, and local storage path.
/// </summary>
public sealed class InstallationMetadata
{
    public bool SetupCompleted { get; set; }
    public int ConfigurationVersion { get; set; } = 1;
    public string InstallationId { get; set; } = Guid.NewGuid().ToString("D");
    public string StoragePath { get; set; } = string.Empty;
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
    public DateTime? SetupCompletedAt { get; set; }
    public string ApplicationVersion { get; set; } = "1.0.0";
}
