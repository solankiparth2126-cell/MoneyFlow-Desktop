using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace MoneyFlow.Tests.InstallerTests;

public class Phase33InstallerTests
{
    private string GetSolutionRoot()
    {
        // Navigate up from bin/Debug/net8.0-windows to solution root
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "MoneyFlow.sln")))
        {
            var parent = Directory.GetParent(dir);
            dir = parent?.FullName;
        }

        return dir ?? throw new InvalidOperationException("Could not locate solution root directory containing MoneyFlow.sln.");
    }

    [Fact]
    public void InnoSetupScript_AdheresToMasterPrompt_Section58()
    {
        // Arrange
        var root = GetSolutionRoot();
        var scriptPath = Path.Combine(root, "installer", "MoneyFlowSetup.iss");

        // Act & Assert: Script must exist
        File.Exists(scriptPath).Should().BeTrue("Inno Setup script must exist in installer/MoneyFlowSetup.iss");

        var content = File.ReadAllText(scriptPath);

        // Section 58 requirements:
        content.Should().Contain("OutputBaseFilename=MoneyFlowSetup", "Installer executable must be named MoneyFlowSetup.exe");
        content.Should().Contain(@"DefaultDirName={autopf}\MoneyFlow", @"Installation directory must target C:\Program Files\MoneyFlow");
        content.Should().Contain("{group}", "Must create Start Menu shortcuts");
        content.Should().Contain("{autodesktop}", "Must support Desktop shortcut");
        content.Should().Contain("MoneyFlow.Desktop.exe", "Main executable must be MoneyFlow.Desktop.exe");
    }

    [Fact]
    public void BuildInstallerScript_ExistsAndConfiguresPackaging()
    {
        // Arrange
        var root = GetSolutionRoot();
        var scriptPath = Path.Combine(root, "installer", "build-installer.ps1");

        // Act & Assert
        File.Exists(scriptPath).Should().BeTrue("build-installer.ps1 script must exist");
        var content = File.ReadAllText(scriptPath);
        content.Should().Contain("dotnet publish", "Build script must run dotnet publish");
        content.Should().Contain("ISCC.exe", "Build script must look for Inno Setup compiler");
    }

    [Fact]
    public void DatabaseManagementScripts_AdhereToMasterPrompt_Section61()
    {
        // Arrange
        var root = GetSolutionRoot();
        var sqlDir = Path.Combine(root, "database", "scripts");

        Directory.Exists(sqlDir).Should().BeTrue("Database scripts directory must exist");

        var ddlScript = Path.Combine(sqlDir, "01_CreateDatabaseAndTables.sql");
        var indexScript = Path.Combine(sqlDir, "02_CreateIndexesAndConstraints.sql");
        var seedScript = Path.Combine(sqlDir, "03_SeedSystemData.sql");
        var backupScript = Path.Combine(sqlDir, "04_BackupAndRestore.sql");

        File.Exists(ddlScript).Should().BeTrue("01_CreateDatabaseAndTables.sql must exist");
        File.Exists(indexScript).Should().BeTrue("02_CreateIndexesAndConstraints.sql must exist");
        File.Exists(seedScript).Should().BeTrue("03_SeedSystemData.sql must exist");
        File.Exists(backupScript).Should().BeTrue("04_BackupAndRestore.sql must exist");

        // Check content coverage
        var ddlText = File.ReadAllText(ddlScript);
        ddlText.Should().Contain("CREATE DATABASE [MoneyFlowDB]");
        ddlText.Should().Contain("CREATE TABLE [dbo].[Companies]");
        ddlText.Should().Contain("CREATE TABLE [dbo].[FinancialYears]");
        ddlText.Should().Contain("CREATE TABLE [dbo].[Groups]");
        ddlText.Should().Contain("CREATE TABLE [dbo].[Ledgers]");
        ddlText.Should().Contain("CREATE TABLE [dbo].[Vouchers]");
        ddlText.Should().Contain("CREATE TABLE [dbo].[VoucherEntries]");

        var indexText = File.ReadAllText(indexScript);
        indexText.Should().Contain("IX_Vouchers_CompanyId");
        indexText.Should().Contain("IX_Vouchers_FinancialYearId");
        indexText.Should().Contain("IX_Vouchers_VoucherDate");
        indexText.Should().Contain("IX_Vouchers_VoucherNumber");

        var seedText = File.ReadAllText(seedScript);
        seedText.Should().Contain("INSERT INTO [dbo].[VoucherTypes]");
        seedText.Should().Contain("INSERT INTO [dbo].[Roles]");
        seedText.Should().Contain("admin");
    }

    [Fact]
    public void ReleasePublishArtifacts_ContainExecutableAndDependencies()
    {
        // Arrange
        var root = GetSolutionRoot();
        var publishExe = Path.Combine(root, "publish", "MoneyFlow.Desktop.exe");

        // Act & Assert
        File.Exists(publishExe).Should().BeTrue("dotnet publish output must include MoneyFlow.Desktop.exe");
        var fileInfo = new FileInfo(publishExe);
        fileInfo.Length.Should().BeGreaterThan(10000, "Executable must be a non-empty compiled binary");
    }
}
