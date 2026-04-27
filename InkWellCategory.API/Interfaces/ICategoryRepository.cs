using InkWell.Shared;
using InkWellCategory.API.Models;

namespace InkWellCategory.API.Interfaces;

public interface ICategoryRepository : IBaseRepository<Category>
{
    Task<Category?> GetByCategoryIdAsync(Guid categoryId);
    Task<Category?> GetBySlugAsync(string slug);
    Task<IEnumerable<Category>> GetRootCategoriesAsync();
    Task<IEnumerable<Category>> GetChildCategoriesAsync(Guid parentId);
    Task<bool> SlugExistsAsync(string slug);

    // tag operations
    Task<Tag?> GetTagByIdAsync(Guid tagId);
    Task<Tag?> GetTagBySlugAsync(string slug);
    Task<IEnumerable<Tag>> GetAllTagsAsync();
    Task<Tag> AddTagAsync(Tag tag);
    Task DeleteTagAsync(Guid tagId);
    Task<bool> TagSlugExistsAsync(string slug);
    Task<IEnumerable<Tag>> GetTrendingTagsAsync(int count);

    // post-category association
    Task<IEnumerable<Category>> GetCategoriesByPostIdAsync(Guid postId);
    Task AddPostCategoryAsync(PostCategory postCategory);
    Task RemovePostCategoryAsync(Guid postId, Guid categoryId);
    Task<bool> PostCategoryExistsAsync(Guid postId, Guid categoryId);

    // post-tag association
    Task<IEnumerable<Tag>> GetTagsByPostIdAsync(Guid postId);
    Task AddPostTagAsync(PostTag postTag);
    Task RemovePostTagAsync(Guid postId, Guid tagId);
    Task<bool> PostTagExistsAsync(Guid postId, Guid tagId);
}