namespace InkWellCategory.API.DTOs;

// Category Requests

public record CreateCategoryRequest(
    string Name,
    string? Description,
    Guid? ParentCategoryId  // null = root category
);

public record UpdateCategoryRequest(
    string Name,
    string? Description,
    Guid? ParentCategoryId
);

// Tag Requests

public record CreateTagRequest(
    string Name
);

// Category Responses

public record CategoryResponse(
    Guid CategoryId,
    string Name,
    string Slug,
    string? Description,
    Guid? ParentCategoryId,
    int PostCount,
    DateTime CreatedAt,
    List<CategoryResponse>? Children  // nested child categories
);

public record CategorySummaryResponse(
    Guid CategoryId,
    string Name,
    string Slug,
    Guid? ParentCategoryId,
    int PostCount
);

// Tag Responses

public record TagResponse(
    Guid TagId,
    string Name,
    string Slug,
    int PostCount,
    DateTime CreatedAt
);