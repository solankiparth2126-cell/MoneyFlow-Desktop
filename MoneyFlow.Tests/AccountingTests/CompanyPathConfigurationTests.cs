using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using MoneyFlow.Core.Common;
using MoneyFlow.Data.Storage;
using Xunit;

namespace MoneyFlow.Tests.AccountingTests;

public class CompanyPathConfigurationTests : IDisposable
{
    private readonly string _testTempDir;

    public CompanyPathConfigurationTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "MoneyFlow_PathConfigTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);
        CompanyPathConfiguration.Reset();
    }

    public void Dispose()
    {
        CompanyPathConfiguration.Reset();
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void EnsureTrailingSeparator_Should_Append_Slash_When_Missing()
    {
        string pathWithoutSlash = @"C:\Custom\Data\Path";
        string result = CompanyPathConfiguration.EnsureTrailingSeparator(pathWithoutSlash);
        result.Should().EndWith(Path.DirectorySeparatorChar.ToString());
    }

    [Fact]
    public void EnsureTrailingSeparator_Should_Not_Duplicate_Slash()
    {
        string pathWithSlash = @"C:\Custom\Data\Path\";
        string result = CompanyPathConfiguration.EnsureTrailingSeparator(pathWithSlash);
        result.Should().Be(@"C:\Custom\Data\Path\");
    }

    [Fact]
    public void SetConfiguredPaths_Should_Directly_Affect_ResolveCompaniesPath()
    {
        string customCompanies = Path.Combine(_testTempDir, "CustomCompanies") + Path.DirectorySeparatorChar;
        string customRoot = _testTempDir;

        CompanyPathConfiguration.SetConfiguredPaths(customRoot, customCompanies);

        CompanyPathConfiguration.ConfiguredCompaniesPath.Should().Be(customCompanies);
        CompanyPathConfiguration.ConfiguredDataRootPath.Should().Be(customRoot);

        string resolved = CompanyPathConfiguration.ResolveCompaniesPath();
        resolved.Should().Be(customCompanies);

        string resolvedRoot = CompanyPathConfiguration.ResolveDataRootPath();
        resolvedRoot.Should().Be(customRoot);
    }

    [Fact]
    public void ResolveCompaniesPath_WithCustomOverride_Should_Return_Override()
    {
        string overridePath = @"D:\ERP_Data\CustomCompanies";
        string resolved = CompanyPathConfiguration.ResolveCompaniesPath(overridePath);

        resolved.Should().StartWith(@"D:\ERP_Data\CustomCompanies");
        resolved.Should().EndWith(Path.DirectorySeparatorChar.ToString());
    }

    [Fact]
    public void CompanyStorageEngine_GetCompaniesRootDirectory_Should_Respect_Configured_Root()
    {
        string customRoot = Path.Combine(_testTempDir, "EnterpriseData");
        var crypto = new CompanyDataCrypto();
        var storage = new CompanyStorageEngine(crypto, customRoot);

        storage.GetDataRootDirectory().Should().Be(customRoot);
        storage.GetCompaniesRootDirectory().Should().Be(CompanyPathConfiguration.EnsureTrailingSeparator(customRoot));
    }

    [Fact]
    public void CompanyStorageEngine_GetCompanyDirectory_Should_Match_Both_COMP_And_Numeric_Prefix()
    {
        string customRoot = Path.Combine(_testTempDir, "StoreRoot");
        string companiesDir = Path.Combine(customRoot, "Companies");
        Directory.CreateDirectory(companiesDir);

        // Create directory in Tally style: 010002_KavanTechnologies
        string kavanDir = Path.Combine(companiesDir, "010002_KavanTechnologies");
        Directory.CreateDirectory(kavanDir);

        var crypto = new CompanyDataCrypto();
        var storage = new CompanyStorageEngine(crypto, customRoot);

        // Querying with numeric code
        string found1 = storage.GetCompanyDirectory("010002");
        found1.Should().Be(kavanDir);

        // Querying with COMP- prefix
        string found2 = storage.GetCompanyDirectory("COMP-010002");
        found2.Should().Be(kavanDir);

        // Querying with stripped number 10002
        string found3 = storage.GetCompanyDirectory("COMP-10002");
        found3.Should().Be(kavanDir);
    }
}
