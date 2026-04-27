namespace InkWell.Shared.DTOs;

// standard wrapper for all api responses so the client always gets a consistent shape
// errors list is optional, only populated on validation failures
public record ApiResponse<T>(bool Success, string Message, T? Data, List<string>? Errors = null);

// paged result for list endpoints - keeps pagination info alongside the items
public record PagedResult<T>(IEnumerable<T> Items, int TotalCount, int Page, int PageSize);
