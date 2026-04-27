using InkWellCategory.API.DTOs;

namespace InkWellCategory.API.Interfaces;

public interface ICategoryService
{
    // category operations
    Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request);
    Task<CategoryResponse> GetCategoryBySlugAsync(string slug);
    Task<CategoryResponse> GetCategoryByIdAsync(Guid categoryId);
    Task<IEnumerable<CategoryResponse>> GetAllCategoriesAsync();
    Task<CategoryResponse> UpdateCategoryAsync(Guid categoryId, UpdateCategoryRequest request);
    Task DeleteCategoryAsync(Guid categoryId);

    // tag operations
    Task<TagResponse> CreateTagAsync(CreateTagRequest request);
    Task<TagResponse> GetTagBySlugAsync(string slug);
    Task<IEnumerable<TagResponse>> GetAllTagsAsync();
    Task<IEnumerable<TagResponse>> GetTrendingTagsAsync(int count = 10);
    Task DeleteTagAsync(Guid tagId);

    // post-category association
    Task AssignCategoryToPostAsync(Guid postId, Guid categoryId);
    Task RemoveCategoryFromPostAsync(Guid postId, Guid categoryId);
    Task<IEnumerable<CategorySummaryResponse>> GetCategoriesByPostAsync(Guid postId);

    // post-tag association
    Task AddTagToPostAsync(Guid postId, Guid tagId);
    Task RemoveTagFromPostAsync(Guid postId, Guid tagId);
    Task<IEnumerable<TagResponse>> GetTagsByPostAsync(Guid postId);
}