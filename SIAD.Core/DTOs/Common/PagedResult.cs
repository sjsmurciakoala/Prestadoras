using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Common;

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }

    public PagedResult()
    {
    }

    public PagedResult(IReadOnlyList<T> items, int totalCount)
    {
        Items = items ?? Array.Empty<T>();
        TotalCount = totalCount;
    }
}
