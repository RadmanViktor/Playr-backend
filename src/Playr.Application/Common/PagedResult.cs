namespace Playr.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, bool HasMore);
