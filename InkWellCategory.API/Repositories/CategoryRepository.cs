using InkWellCategory.API.Data;
using InkWellCategory.API.Interfaces;
using InkWellCategory.API.Models;
using Microsoft.EntityFrameworkCore;

namespace InkWellCategory.API.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly CategoryDbContext _db;
    public CategoryRepository(CategoryDbContext db) => _db = db;

    // Category CRUD

    public async Task<Category?> GetByIdAsync(Guid id) =>
        await _db.Categories.FirstOrDefaultAsync(c => c.CategoryId == id);

    public async Task<Category?> GetByCategoryIdAsync(Guid categoryId) =>
        await _db.Categories.FirstOrDefaultAsync(c => c.CategoryId == categoryId);

    public async Task<Category?> GetBySlugAsync(string slug) =>
        await _db.Categories.FirstOrDefaultAsync(c => c.Slug == slug);

    public async Task<IEnumerable<Category>> GetAllAsync() =>
        await _db.Categories.ToListAsync();

    public async Task<IEnumerable<Category>> GetRootCategoriesAsync() =>
        await _db.Categories
            .Where(c => c.ParentCategoryId == null)
            .ToListAsync();

    public async Task<IEnumerable<Category>> GetChildCategoriesAsync(Guid parentId) =>
        await _db.Categories
            .Where(c => c.ParentCategoryId == parentId)
            .ToListAsync();

    public async Task<bool> SlugExistsAsync(string slug) =>
        await _db.Categories.AnyAsync(c => c.Slug == slug);

    public async Task<Category> AddAsync(Category entity)
    {
        _db.Categories.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<Category> UpdateAsync(Category entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _db.Categories.Update(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(Guid id)
    {
        var category = await GetByCategoryIdAsync(id);
        if (category is null) return;
        category.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id) =>
        await _db.Categories.AnyAsync(c => c.CategoryId == id);

    // Tag CRUD

    public async Task<Tag?> GetTagByIdAsync(Guid tagId) =>
        await _db.Tags.FirstOrDefaultAsync(t => t.TagId == tagId);

    public async Task<Tag?> GetTagBySlugAsync(string slug) =>
        await _db.Tags.FirstOrDefaultAsync(t => t.Slug == slug);

    public async Task<IEnumerable<Tag>> GetAllTagsAsync() =>
        await _db.Tags.OrderBy(t => t.Name).ToListAsync();

    public async Task<Tag> AddTagAsync(Tag tag)
    {
        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();
        return tag;
    }

    public async Task<Tag> UpdateTagAsync(Tag tag)
    {
        _db.Tags.Update(tag);
        await _db.SaveChangesAsync();
        return tag;
    }

    public async Task DeleteTagAsync(Guid tagId)
    {
        var tag = await GetTagByIdAsync(tagId);
        if (tag is null) return;
        tag.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    public async Task<bool> TagSlugExistsAsync(string slug) =>
        await _db.Tags.AnyAsync(t => t.Slug == slug);

    // trending = highest PostCount
    public async Task<IEnumerable<Tag>> GetTrendingTagsAsync(int count) =>
        await _db.Tags
            .OrderByDescending(t => t.PostCount)
            .Take(count)
            .ToListAsync();

    // Post-Category Association

    public async Task<IEnumerable<Category>> GetCategoriesByPostIdAsync(Guid postId)
    {
        var categoryIds = await _db.PostCategories
            .Where(pc => pc.PostId == postId)
            .Select(pc => pc.CategoryId)
            .ToListAsync();

        return await _db.Categories
            .Where(c => categoryIds.Contains(c.CategoryId))
            .ToListAsync();
    }

    public async Task AddPostCategoryAsync(PostCategory postCategory)
    {
        _db.PostCategories.Add(postCategory);
        await _db.SaveChangesAsync();
    }

    public async Task RemovePostCategoryAsync(Guid postId, Guid categoryId)
    {
        var record = await _db.PostCategories
            .FirstOrDefaultAsync(pc => pc.PostId == postId
                                    && pc.CategoryId == categoryId);
        if (record is null) return;
        _db.PostCategories.Remove(record);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> PostCategoryExistsAsync(Guid postId, Guid categoryId) =>
        await _db.PostCategories
            .AnyAsync(pc => pc.PostId == postId && pc.CategoryId == categoryId);

    // Post-Tag Association

    public async Task<IEnumerable<Tag>> GetTagsByPostIdAsync(Guid postId)
    {
        var tagIds = await _db.PostTags
            .Where(pt => pt.PostId == postId)
            .Select(pt => pt.TagId)
            .ToListAsync();

        return await _db.Tags
            .Where(t => tagIds.Contains(t.TagId))
            .ToListAsync();
    }

    public async Task AddPostTagAsync(PostTag postTag)
    {
        _db.PostTags.Add(postTag);
        await _db.SaveChangesAsync();
    }

    public async Task RemovePostTagAsync(Guid postId, Guid tagId)
    {
        var record = await _db.PostTags
            .FirstOrDefaultAsync(pt => pt.PostId == postId
                                    && pt.TagId == tagId);
        if (record is null) return;
        _db.PostTags.Remove(record);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> PostTagExistsAsync(Guid postId, Guid tagId) =>
        await _db.PostTags
            .AnyAsync(pt => pt.PostId == postId && pt.TagId == tagId);
}