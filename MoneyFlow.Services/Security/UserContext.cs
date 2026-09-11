using System;
using System.Collections.Generic;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Services.Security;

public class UserContext : IUserContext
{
    private readonly object _lock = new();
    private readonly HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);

    public int? UserId { get; private set; }
    public string Username { get; private set; } = "admin";
    public string FullName { get; private set; } = "Administrator";
    public string RoleName { get; private set; } = "Administrator";
    public bool IsAuthenticated { get; private set; } = true;
    public IReadOnlySet<string> Permissions => _permissions;

    public event Action? OnUserChanged;

    public bool HasPermission(string permissionKey)
    {
        lock (_lock)
        {
            // Administrator has superuser access to all features
            if (string.Equals(RoleName, "Administrator", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return _permissions.Contains(permissionKey);
        }
    }

    public void SetCurrentUser(int userId, string username, string fullName, string roleName, IEnumerable<string> permissions)
    {
        lock (_lock)
        {
            UserId = userId;
            Username = username;
            FullName = fullName;
            RoleName = roleName;
            IsAuthenticated = true;

            _permissions.Clear();
            foreach (var p in permissions)
            {
                _permissions.Add(p);
            }
        }

        OnUserChanged?.Invoke();
    }

    public void Clear()
    {
        lock (_lock)
        {
            UserId = null;
            Username = string.Empty;
            FullName = string.Empty;
            RoleName = string.Empty;
            IsAuthenticated = false;
            _permissions.Clear();
        }

        OnUserChanged?.Invoke();
    }
}
