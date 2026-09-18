using System;

namespace MoneyFlow.Data;

/// <summary>Base exception for company file operations.</summary>
public class CompanyFileException : Exception
{
    public CompanyFileException(string message) : base(message) { }
    public CompanyFileException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Thrown when company.data is corrupted or has invalid structure.</summary>
public class CompanyFileCorruptedException : CompanyFileException
{
    public CompanyFileCorruptedException(string message) : base(message) { }
    public CompanyFileCorruptedException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Thrown when password verification fails or DEK cannot be unwrapped.</summary>
public class CompanyAuthenticationException : CompanyFileException
{
    public CompanyAuthenticationException() : base("Incorrect company password or authentication failed.") { }
    public CompanyAuthenticationException(string message) : base(message) { }
}

/// <summary>Thrown when company.data was created with a newer version of MyERP.</summary>
public class UnsupportedCompanyVersionException : CompanyFileException
{
    public int FileVersion { get; }
    public int AppVersion { get; }

    public UnsupportedCompanyVersionException(int fileVersion, int appVersion)
        : base($"This company was created with a newer version of MyERP (file: v{fileVersion}, app: v{appVersion}). Please upgrade MyERP before opening this company.")
    {
        FileVersion = fileVersion;
        AppVersion = appVersion;
    }
}

/// <summary>Thrown when a data migration fails.</summary>
public class CompanyMigrationException : CompanyFileException
{
    public int FromVersion { get; }
    public int ToVersion { get; }

    public CompanyMigrationException(int from, int to, string message)
        : base($"Migration from v{from} to v{to} failed: {message}")
    {
        FromVersion = from;
        ToVersion = to;
    }

    public CompanyMigrationException(int from, int to, string message, Exception inner)
        : base($"Migration from v{from} to v{to} failed: {message}", inner)
    {
        FromVersion = from;
        ToVersion = to;
    }
}

/// <summary>Thrown when a company file is locked by another process/session.</summary>
public class CompanyLockException : CompanyFileException
{
    public CompanyLockException(string companyPath)
        : base($"Company file is locked by another process: {companyPath}") { }
}

/// <summary>Thrown when a restore operation fails.</summary>
public class CompanyRestoreException : CompanyFileException
{
    public CompanyRestoreException(string message) : base(message) { }
    public CompanyRestoreException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Thrown when transaction recovery encounters an unrecoverable state.</summary>
public class TransactionRecoveryException : CompanyFileException
{
    public TransactionRecoveryException(string message) : base(message) { }
    public TransactionRecoveryException(string message, Exception inner) : base(message, inner) { }
}
