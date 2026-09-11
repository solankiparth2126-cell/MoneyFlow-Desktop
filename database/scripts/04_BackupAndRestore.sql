-- =========================================================================
-- MoneyFlow Desktop ERP — Backup & Restore Administration Script
-- Master Prompt Section 44, 45 & 61
-- =========================================================================

USE [master];
GO

-- 1. Automated Full Database Backup Stored Procedure
IF OBJECT_ID(N'dbo.usp_BackupMoneyFlowDB', N'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_BackupMoneyFlowDB;
GO

CREATE PROCEDURE dbo.usp_BackupMoneyFlowDB
    @BackupDirectory NVARCHAR(500) = NULL,
    @BackupFilePath NVARCHAR(1000) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Default to user's Documents\MoneyFlow\Backups folder if directory is not provided
    IF @BackupDirectory IS NULL OR @BackupDirectory = ''
    BEGIN
        SET @BackupDirectory = N'C:\ProgramData\MoneyFlow\Backups';
    END

    -- Ensure directory format
    IF RIGHT(@BackupDirectory, 1) <> '\'
        SET @BackupDirectory = @BackupDirectory + '\';

    DECLARE @Timestamp NVARCHAR(30) = REPLACE(REPLACE(REPLACE(CONVERT(NVARCHAR(30), GETDATE(), 120), '-', ''), ':', ''), ' ', '_');
    SET @BackupFilePath = @BackupDirectory + N'MoneyFlowDB_' + @Timestamp + N'.bak';

    PRINT 'Starting Full Backup to: ' + @BackupFilePath;

    BACKUP DATABASE [MoneyFlowDB]
    TO DISK = @BackupFilePath
    WITH FORMAT,
         INIT,
         NAME = N'MoneyFlowDB-Full Database Backup',
         SKIP,
         NOREWIND,
         NOUNLOAD,
         STATS = 10,
         CHECKSUM;

    PRINT 'Full Backup Completed Successfully.';
END;
GO

-- 2. Database Restore Stored Procedure (Closes active connections before restore)
IF OBJECT_ID(N'dbo.usp_RestoreMoneyFlowDB', N'P') IS NOT NULL
    DROP PROCEDURE dbo.usp_RestoreMoneyFlowDB;
GO

CREATE PROCEDURE dbo.usp_RestoreMoneyFlowDB
    @BackupFilePath NVARCHAR(1000)
AS
BEGIN
    SET NOCOUNT ON;

    IF @BackupFilePath IS NULL OR @BackupFilePath = ''
    BEGIN
        RAISERROR(N'Backup file path cannot be null or empty.', 16, 1);
        RETURN;
    END

    PRINT 'Setting MoneyFlowDB to SINGLE_USER mode to close existing connections...';
    ALTER DATABASE [MoneyFlowDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

    PRINT 'Restoring MoneyFlowDB from: ' + @BackupFilePath;
    RESTORE DATABASE [MoneyFlowDB]
    FROM DISK = @BackupFilePath
    WITH REPLACE,
         RECOVERY,
         STATS = 10;

    PRINT 'Resetting MoneyFlowDB to MULTI_USER mode...';
    ALTER DATABASE [MoneyFlowDB] SET MULTI_USER;

    PRINT 'Database Restore Completed Successfully.';
END;
GO
