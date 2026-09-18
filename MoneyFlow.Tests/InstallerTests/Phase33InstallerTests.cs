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
    public void DatabaseManagementScripts_VerifiedRemovedForFileBasedArchitecture()
    {
        // Arrange & Act
        var root = GetSolutionRoot();
        var sqlDir = Path.Combine(root, "database", "scripts");

        // Assert: SQL scripts directory must NOT exist per Section 11 SQL Removal
        Directory.Exists(sqlDir).Should().BeFalse("database/scripts directory must be removed for 100% file-based ERP architecture");
    }

    [Fact]
    public void BuildArtifacts_ContainExecutableAndDependencies()
    {
        // Arrange
        var root = GetSolutionRoot();
        var binExe = Path.Combine(root, "MoneyFlow.Desktop", "bin", "Debug", "net8.0-windows", "MoneyFlow.Desktop.exe");
        var publishExe = Path.Combine(root, "publish", "MoneyFlow.Desktop.exe");

        // Act & Assert
        (File.Exists(binExe) || File.Exists(publishExe)).Should().BeTrue("Application output must include MoneyFlow.Desktop.exe");
    }
}
