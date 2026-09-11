using System;
using System.Collections.Generic;

namespace MoneyFlow.Core.Interfaces;

public interface IUserContext
{
    int? UserId { get; }
    string Username { get; }
    string FullName { get; }
    string RoleName { get; }
    bool IsAuthenticated { get; }
    IReadOnlySet<string> Permissions { get; }

    bool HasPermission(string permissionKey);
    void SetCurrentUser(int userId, string username, string fullName, string roleName, IEnumerable<string> permissions);
    void Clear();

    event Action? OnUserChanged;
}
