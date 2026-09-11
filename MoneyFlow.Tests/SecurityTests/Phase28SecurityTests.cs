using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Data;
using MoneyFlow.Services.Security;
using Xunit;

namespace MoneyFlow.Tests.SecurityTests;

public class Phase28SecurityTests
{
    private (AppDbContext Context, UserContext UserCtx, AuditService AuditSvc, SecurityService SecSvc) CreateTestSetup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var userContext = new UserContext();
        var auditService = new AuditService(context, userContext, NullLogger<AuditService>.Instance);
        var securityService = new SecurityService(context, userContext, auditService, NullLogger<SecurityService>.Instance);

        return (context, userContext, auditService, securityService);
    }

    private async Task SeedRolesAndPermissionsAsync(AppDbContext context)
    {
        var p1 = new Permission { PermissionKey = "Voucher.Create", Module = "Transactions", Description = "Create vouchers" };
        var p2 = new Permission { PermissionKey = "Voucher.View", Module = "Transactions", Description = "View vouchers" };
        var p3 = new Permission { PermissionKey = "Ledger.Create", Module = "Masters", Description = "Create ledgers" };
        var p4 = new Permission { PermissionKey = "Report.Financial", Module = "Reports", Description = "Financial statements" };

        context.Permissions.AddRange(p1, p2, p3, p4);
        await context.SaveChangesAsync();

        var adminRole = new Role
        {
            RoleName = "Administrator",
            Description = "Full access",
            Permissions = new List<Permission> { p1, p2, p3, p4 }
        };

        var accountantRole = new Role
        {
            RoleName = "Accountant",
            Description = "Standard accounting",
            Permissions = new List<Permission> { p1, p2, p3 }
        };

        var viewerRole = new Role
        {
            RoleName = "Viewer",
            Description = "Read-only access",
            Permissions = new List<Permission> { p2, p4 }
        };

        context.Roles.AddRange(adminRole, accountantRole, viewerRole);
        await context.SaveChangesAsync();
    }

    [Fact]
    public void PasswordHasher_HashesAndVerifiesPasswordCorrectly()
    {
        // Arrange
        string password = "SecretAccountingPassword#2026";

        // Act
        var (hash, salt) = PasswordHasher.HashPassword(password);

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
        salt.Should().NotBeNullOrWhiteSpace();
        hash.Length.Should().Be(64); // 256-bit hex
        salt.Length.Should().Be(32); // 128-bit hex

        // Verification
        PasswordHasher.VerifyPassword(password, hash, salt).Should().BeTrue();
        PasswordHasher.VerifyPassword("WrongPassword123", hash, salt).Should().BeFalse();
        PasswordHasher.VerifyPassword(string.Empty, hash, salt).Should().BeFalse();
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidCredentials_SucceedsAndSetsContext()
    {
        // Arrange
        var (context, userContext, _, securityService) = CreateTestSetup();
        await SeedRolesAndPermissionsAsync(context);

        var accountantRole = await context.Roles.FirstAsync(r => r.RoleName == "Accountant");
        var (hash, salt) = PasswordHasher.HashPassword("AccountantPass456");

        var user = new User
        {
            Username = "john_doe",
            FullName = "John Doe, CPA",
            Email = "john@example.com",
            RoleId = accountantRole.RoleId,
            PasswordHash = hash,
            Salt = salt,
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Act
        var result = await securityService.AuthenticateAsync("john_doe", "AccountantPass456");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Username.Should().Be("john_doe");
        result.FullName.Should().Be("John Doe, CPA");
        result.RoleName.Should().Be("Accountant");
        result.Permissions.Should().Contain("Voucher.Create");

        // Verify UserContext was updated
        userContext.IsAuthenticated.Should().BeTrue();
        userContext.Username.Should().Be("john_doe");
        userContext.HasPermission("Voucher.Create").Should().BeTrue();
        userContext.HasPermission("Report.Financial").Should().BeFalse(); // Not in accountant role
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidPassword_Fails()
    {
        // Arrange
        var (context, _, _, securityService) = CreateTestSetup();
        await SeedRolesAndPermissionsAsync(context);

        var adminRole = await context.Roles.FirstAsync(r => r.RoleName == "Administrator");
        var (hash, salt) = PasswordHasher.HashPassword("correct_password");

        context.Users.Add(new User
        {
            Username = "valid_user",
            RoleId = adminRole.RoleId,
            PasswordHash = hash,
            Salt = salt,
            IsActive = true
        });
        await context.SaveChangesAsync();

        // Act
        var result = await securityService.AuthenticateAsync("valid_user", "incorrect_password");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task AuthenticateAsync_WithInactiveUser_Fails()
    {
        // Arrange
        var (context, _, _, securityService) = CreateTestSetup();
        await SeedRolesAndPermissionsAsync(context);

        var role = await context.Roles.FirstAsync();
        var (hash, salt) = PasswordHasher.HashPassword("any_password");

        context.Users.Add(new User
        {
            Username = "disabled_user",
            RoleId = role.RoleId,
            PasswordHash = hash,
            Salt = salt,
            IsActive = false
        });
        await context.SaveChangesAsync();

        // Act
        var result = await securityService.AuthenticateAsync("disabled_user", "any_password");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("inactive");
    }

    [Fact]
    public async Task CreateUserAsync_WithDuplicateUsername_ThrowsException()
    {
        // Arrange
        var (context, _, _, securityService) = CreateTestSetup();
        await SeedRolesAndPermissionsAsync(context);
        var role = await context.Roles.FirstAsync();

        await securityService.CreateUserAsync(new UserCreateDto
        {
            Username = "clerk",
            FullName = "First Clerk",
            Password = "password123",
            RoleId = role.RoleId
        });

        // Act & Assert
        var act = async () => await securityService.CreateUserAsync(new UserCreateDto
        {
            Username = "clerk",
            FullName = "Second Clerk",
            Password = "password456",
            RoleId = role.RoleId
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already taken*");
    }

    [Fact]
    public async Task UpdateUserAsync_UpdatesDetailsSuccessfully()
    {
        // Arrange
        var (context, _, _, securityService) = CreateTestSetup();
        await SeedRolesAndPermissionsAsync(context);

        var adminRole = await context.Roles.FirstAsync(r => r.RoleName == "Administrator");
        var viewerRole = await context.Roles.FirstAsync(r => r.RoleName == "Viewer");

        var user = await securityService.CreateUserAsync(new UserCreateDto
        {
            Username = "operator1",
            FullName = "Original Name",
            Email = "orig@test.com",
            Password = "password123",
            RoleId = adminRole.RoleId
        });

        // Act
        var updated = await securityService.UpdateUserAsync(new UserUpdateDto
        {
            UserId = user.UserId,
            FullName = "Updated Senior Name",
            Email = "senior@test.com",
            RoleId = viewerRole.RoleId,
            IsActive = true
        });

        // Assert
        updated.FullName.Should().Be("Updated Senior Name");
        updated.Email.Should().Be("senior@test.com");
        updated.RoleName.Should().Be("Viewer");
    }

    [Fact]
    public async Task ToggleUserStatusAsync_PreventsDeactivatingMasterAdmin()
    {
        // Arrange
        var (context, _, _, securityService) = CreateTestSetup();
        await SeedRolesAndPermissionsAsync(context);
        var role = await context.Roles.FirstAsync();

        var (hash, salt) = PasswordHasher.HashPassword("admin123");
        var admin = new User
        {
            Username = "admin",
            FullName = "System Administrator",
            RoleId = role.RoleId,
            PasswordHash = hash,
            Salt = salt,
            IsActive = true
        };
        context.Users.Add(admin);
        await context.SaveChangesAsync();

        // Act & Assert
        var act = async () => await securityService.ToggleUserStatusAsync(admin.UserId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be deactivated*");
    }

    [Fact]
    public async Task UpdateRolePermissionsAsync_UpdatesPermissionsCorrectly()
    {
        // Arrange
        var (context, _, _, securityService) = CreateTestSetup();
        await SeedRolesAndPermissionsAsync(context);

        var viewerRole = await context.Roles.FirstAsync(r => r.RoleName == "Viewer");

        // Act: grant Voucher.Create to Viewer role
        await securityService.UpdateRolePermissionsAsync(new RolePermissionsUpdateDto
        {
            RoleId = viewerRole.RoleId,
            PermissionKeys = new List<string> { "Voucher.Create", "Voucher.View" }
        });

        // Assert
        var permissions = await securityService.GetAllPermissionsAsync(viewerRole.RoleId);
        permissions.First(p => p.PermissionKey == "Voucher.Create").IsGranted.Should().BeTrue();
        permissions.First(p => p.PermissionKey == "Voucher.View").IsGranted.Should().BeTrue();
        permissions.First(p => p.PermissionKey == "Report.Financial").IsGranted.Should().BeFalse();
    }

    [Fact]
    public async Task AuditService_LogsAndQueriesEventsCorrectly()
    {
        // Arrange
        var (context, userContext, auditService, _) = CreateTestSetup();
        userContext.SetCurrentUser(1, "auditor", "Lead Auditor", "Administrator", new[] { "All" });

        // Act
        await auditService.LogAsync(1, "Create", "Voucher", "VCH-001", "Created payment voucher");
        await auditService.LogAsync(1, "Delete", "Voucher", "VCH-002", "Deleted cancelled voucher");
        await auditService.LogAsync(1, "Backup", "System", "snap.mfb", "Created company backup");

        // Assert
        var allLogs = await auditService.GetLogsAsync(new AuditLogFilterDto());
        allLogs.Should().HaveCount(3);
        allLogs.First().Username.Should().Be("auditor");

        var voucherLogs = await auditService.GetLogsAsync(new AuditLogFilterDto { Module = "Voucher" });
        voucherLogs.Should().HaveCount(2);

        var deleteLogs = await auditService.GetLogsAsync(new AuditLogFilterDto { Action = "Delete" });
        deleteLogs.Should().HaveCount(1);
        deleteLogs[0].RecordId.Should().Be("VCH-002");
    }

    [Fact]
    public void UserContext_AdministratorHasSuperuserAccess()
    {
        // Arrange
        var userContext = new UserContext();
        userContext.SetCurrentUser(1, "admin", "Admin", "Administrator", Array.Empty<string>());

        // Act & Assert
        userContext.HasPermission("Any.NonExistent.Permission").Should().BeTrue();
        userContext.HasPermission("System.Wipe").Should().BeTrue();
    }
}
