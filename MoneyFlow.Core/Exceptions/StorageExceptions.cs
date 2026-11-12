using System;

namespace MoneyFlow.Core.Exceptions;

public class CompanyNotFoundException : Exception
{
    public string CompanyId { get; }

    public CompanyNotFoundException(string companyId) 
        : base($"Company with ID '{companyId}' was not found.")
    {
        CompanyId = companyId;
    }

    public CompanyNotFoundException(string companyId, string message) 
        : base(message)
    {
        CompanyId = companyId;
    }
}

public class CompanyAlreadyExistsException : Exception
{
    public string CompanyId { get; }

    public CompanyAlreadyExistsException(string companyId) 
        : base($"A company with ID '{companyId}' already exists.")
    {
        CompanyId = companyId;
    }

    public CompanyAlreadyExistsException(string companyId, string message) 
        : base(message)
    {
        CompanyId = companyId;
    }
}

public class CompanyLockedException : Exception
{
    public string CompanyId { get; }

    public CompanyLockedException(string companyId) 
        : base($"This company '{companyId}' is currently being used by another MyERP instance.")
    {
        CompanyId = companyId;
    }

    public CompanyLockedException(string companyId, string message) 
        : base(message)
    {
        CompanyId = companyId;
    }
}

public class InvalidCompanyFileException : Exception
{
    public InvalidCompanyFileException(string message) : base(message) { }
    public InvalidCompanyFileException(string message, Exception inner) : base(message, inner) { }
}

public class UnsupportedFileFormatException : Exception
{
    public int DetectedVersion { get; }
    public int SupportedVersion { get; }

    public UnsupportedFileFormatException(int detected, int supported) 
        : base($"Unsupported file format version {detected}. Highest supported version is {supported}.")
    {
        DetectedVersion = detected;
        SupportedVersion = supported;
    }
}

public class UnsupportedEncryptionVersionException : Exception
{
    public int DetectedVersion { get; }

    public UnsupportedEncryptionVersionException(int detected) 
        : base($"Unsupported encryption version {detected}. Please update MyERP.")
    {
        DetectedVersion = detected;
    }
}

public class UnsupportedDataVersionException : Exception
{
    public int FileDataVersion { get; }
    public int ApplicationSupportedVersion { get; }

    public UnsupportedDataVersionException(int fileVersion, int appVersion) 
        : base($"This company data (Version {fileVersion}) was created by a newer version of MyERP (Supported: Version {appVersion}). Please upgrade MyERP before opening this company.")
    {
        FileDataVersion = fileVersion;
        ApplicationSupportedVersion = appVersion;
    }
}

public class InvalidPasswordException : Exception
{
    public InvalidPasswordException() 
        : base("Incorrect company password. Decryption failed.") { }

    public InvalidPasswordException(string message) 
        : base(message) { }
}

public class CompanyDataTamperedException : Exception
{
    public CompanyDataTamperedException() 
        : base("The company data file is invalid or has been modified. Authentication tag verification failed.") { }

    public CompanyDataTamperedException(string message) 
        : base(message) { }

    public CompanyDataTamperedException(string message, Exception inner) 
        : base(message, inner) { }
}

public class CompanyDataCorruptedException : Exception
{
    public CompanyDataCorruptedException(string message) : base(message) { }
    public CompanyDataCorruptedException(string message, Exception inner) : base(message, inner) { }
}

public class MigrationException : Exception
{
    public int FromVersion { get; }
    public int ToVersion { get; }

    public MigrationException(int fromVersion, int toVersion, string message) 
        : base($"Migration from v{fromVersion} to v{toVersion} failed: {message}")
    {
        FromVersion = fromVersion;
        ToVersion = toVersion;
    }

    public MigrationException(int fromVersion, int toVersion, string message, Exception inner) 
        : base($"Migration from v{fromVersion} to v{toVersion} failed: {message}", inner)
    {
        FromVersion = fromVersion;
        ToVersion = toVersion;
    }
}

public class CompanyValidationException : Exception
{
    public CompanyValidationException(string message) : base(message) { }
    public CompanyValidationException(string message, Exception inner) : base(message, inner) { }
}

public class BackupException : Exception
{
    public BackupException(string message) : base(message) { }
    public BackupException(string message, Exception inner) : base(message, inner) { }
}

public class RestoreException : Exception
{
    public RestoreException(string message) : base(message) { }
    public RestoreException(string message, Exception inner) : base(message, inner) { }
}
