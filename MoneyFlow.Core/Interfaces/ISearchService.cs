using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;

namespace MoneyFlow.Core.Interfaces;

public interface ISearchService
{
    Task<IReadOnlyList<GlobalSearchResultDto>> SearchAsync(
        int companyId,
        string query,
        GlobalSearchCategory category = GlobalSearchCategory.All,
        int maxResults = 50,
        CancellationToken ct = default);
}
