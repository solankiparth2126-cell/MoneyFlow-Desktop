using System;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;

namespace MoneyFlow.Core.Interfaces;

public interface ISettingsService
{
    Task<ApplicationSettingsDto> GetSettingsAsync();
    Task SaveSettingsAsync(ApplicationSettingsDto settings);
    Task<string?> GetSettingValueAsync(string key, string? defaultValue = null);
    Task SetSettingValueAsync(string key, string value, string? description = null);
    Task<bool> IsDateLockedAsync(DateTime date);
    string FormatDate(DateTime date);
    string FormatCurrency(decimal amount);
}
