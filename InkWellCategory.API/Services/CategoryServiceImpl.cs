using InkWellCategory.API.DTOs;
using InkWellCategory.API.Interfaces;
using InkWellCategory.API.Models;

namespace InkWellCategory.API.Services;

public class CategoryServiceImpl : ICategoryService
{
    private readonly ICategoryRepository _categoryRepo;

    public CategoryServiceImpl(ICategoryRepository categoryRepo) =>
        _categoryRepo = categoryRepo;

    // Category Operations

    public async Task<CategoryResponse> CreateCategoryAsync(CreateCategoryRequest request)
    {
        var slug = await GenerateUniqueCategorySlugAsync(request.Name);

        var category = new Category
        {
            Name             = request.Name,
            Slug             = slug,
            Description      = request.Description,
            ParentCategoryId = request.ParentCategoryId
        };

        await _categoryRepo.AddAsync(category);
        return await ToCategoryResponseAsync(category);
    }

    public async Task<CategoryResponse> GetCategoryBySlugAsync(string slug)
    {
        var category = await _categoryRepo.GetBySlugAsync(slug)
            ?? throw new KeyNotFoundException("Category not found.");
        return await ToCategoryResponseAsync(category);
    }

    public async Task<CategoryResponse> GetCategoryByIdAsync(Guid categoryId)
    {
        var category = await _categoryRepo.GetByCategoryIdAsync(categoryId)
            ?? throw new KeyNotFoundException("Category not found.");
        return await ToCategoryResponseAsync(category);
    }

    public async Task<IEnumerable<CategoryResponse>> GetAllCategoriesAsync()
    {
        // return only root categories with children nested inside
        var roots = await _categoryRepo.GetRootCategoriesAsync();
        var result = new List<CategoryResponse>();

        foreach (var root in roots)
            result.Add(await ToCategoryResponseAsync(root));

        return result;
    }

    public async Task<CategoryResponse> UpdateCategoryAsync(
        Guid categoryId, UpdateCategoryRequest request)
    {
        var category = await _categoryRepo.GetByCategoryIdAsync(categoryId)
            ?? throw new KeyNotFoundException("Category not found.");

        category.Name             = request.Name;
        category.Description      = request.Description;
        category.ParentCategoryId = request.ParentCategoryId;

        await _categoryRepo.UpdateAsync(category);
        return await ToCategoryResponseAsync(category);
    }

    public async Task DeleteCategoryAsync(Guid categoryId) =>
        await _categoryRepo.DeleteAsync(categoryId);

    // Tag Operations

    public async Task<TagResponse> CreateTagAsync(CreateTagRequest request)
    {
        var slug = await GenerateUniqueTagSlugAsync(request.Name);

        var tag = new Tag
        {
            Name = request.Name,
            Slug = slug
        };

        await _categoryRepo.AddTagAsync(tag);
        return ToTagResponse(tag);
    }

    public async Task<TagResponse> GetTagBySlugAsync(string slug)
    {
        var tag = await _categoryRepo.GetTagBySlugAsync(slug)
            ?? throw new KeyNotFoundException("Tag not found.");
        return ToTagResponse(tag);
    }

    public async Task<IEnumerable<TagResponse>> GetAllTagsAsync()
    {
        var tags = await _categoryRepo.GetAllTagsAsync();
        return tags.Select(ToTagResponse);
    }

    public async Task<IEnumerable<TagResponse>> GetTrendingTagsAsync(int count = 10)
    {
        var tags = await _categoryRepo.GetTrendingTagsAsync(count);
        return tags.Select(ToTagResponse);
    }

    public async Task DeleteTagAsync(Guid tagId) =>
        await _categoryRepo.DeleteTagAsync(tagId);

    // Post-Category Association

    public async Task AssignCategoryToPostAsync(Guid postId, Guid categoryId)
    {
        var exists = await _categoryRepo.PostCategoryExistsAsync(postId, categoryId);
        if (exists)
            throw new InvalidOperationException("Category already assigned to post.");

        var postCategory = new PostCategory
        {
            PostId     = postId,
            CategoryId = categoryId
        };

        await _categoryRepo.AddPostCategoryAsync(postCategory);

        // increment category post count
        var category = await _categoryRepo.GetByCategoryIdAsync(categoryId);
        if (category is not null)
        {
            category.PostCount++;
            await _categoryRepo.UpdateAsync(category);
        }
    }

    public async Task RemoveCategoryFromPostAsync(Guid postId, Guid categoryId)
    {
        await _categoryRepo.RemovePostCategoryAsync(postId, categoryId);

        // decrement category post count
        var category = await _categoryRepo.GetByCategoryIdAsync(categoryId);
        if (category is not null && category.PostCount > 0)
        {
            category.PostCount--;
            await _categoryRepo.UpdateAsync(category);
        }
    }

    public async Task<IEnumerable<CategorySummaryResponse>> GetCategoriesByPostAsync(Guid postId)
    {
        var categories = await _categoryRepo.GetCategoriesByPostIdAsync(postId);
        return categories.Select(c => new CategorySummaryResponse(
            c.CategoryId, c.Name, c.Slug, c.ParentCategoryId, c.PostCount));
    }

    // Post-Tag Association

    public async Task AddTagToPostAsync(Guid postId, Guid tagId)
    {
        var exists = await _categoryRepo.PostTagExistsAsync(postId, tagId);
        if (exists)
            throw new InvalidOperationException("Tag already assigned to post.");

        var postTag = new PostTag { PostId = postId, TagId = tagId };
        await _categoryRepo.AddPostTagAsync(postTag);

        // increment tag post count
        var tag = await _categoryRepo.GetTagByIdAsync(tagId);
        if (tag is not null)
        {
            tag.PostCount++;
            await _categoryRepo.UpdateTagAsync(tag);
        }
    }

    public async Task RemoveTagFromPostAsync(Guid postId, Guid tagId)
    {
        await _categoryRepo.RemovePostTagAsync(postId, tagId);

        // decrement tag post count
        var tag = await _categoryRepo.GetTagByIdAsync(tagId);
        if (tag is not null && tag.PostCount > 0)
        {
            tag.PostCount--;
            await _categoryRepo.UpdateTagAsync(tag);
        }
    }

    public async Task<IEnumerable<TagResponse>> GetTagsByPostAsync(Guid postId)
    {
        var tags = await _categoryRepo.GetTagsByPostIdAsync(postId);
        return tags.Select(ToTagResponse);
    }

    // Private Helpers

    private async Task<string> GenerateUniqueCategorySlugAsync(string name)
    {
        var baseSlug = GenerateSlug(name);
        var slug = baseSlug;
        var counter = 1;

        while (await _categoryRepo.SlugExistsAsync(slug))
            slug = $"{baseSlug}-{counter++}";

        return slug;
    }

    private async Task<string> GenerateUniqueTagSlugAsync(string name)
    {
        var baseSlug = GenerateSlug(name);
        var slug = baseSlug;
        var counter = 1;

        while (await _categoryRepo.TagSlugExistsAsync(slug))
            slug = $"{baseSlug}-{counter++}";

        return slug;
    }

    // simple slug generator without external package
    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace(".", "-")
            .Replace("/", "-");

    // builds full category response with nested children, guarded against cycles
    private async Task<CategoryResponse> ToCategoryResponseAsync(Category c, HashSet<Guid>? visited = null)
    {
        visited ??= new HashSet<Guid>();
        if (!visited.Add(c.CategoryId))
        {
            // cycle detected: return node without fetching its children again
            return new CategoryResponse(
                c.CategoryId, c.Name, c.Slug, c.Description,
                c.ParentCategoryId, c.PostCount, c.CreatedAt, null);
        }

        var children = await _categoryRepo.GetChildCategoriesAsync(c.CategoryId);
        var childResponses = new List<CategoryResponse>();

        foreach (var child in children)
            childResponses.Add(await ToCategoryResponseAsync(child, visited));

        visited.Remove(c.CategoryId);

        return new CategoryResponse(
            c.CategoryId, c.Name, c.Slug, c.Description,
            c.ParentCategoryId, c.PostCount, c.CreatedAt,
            childResponses.Count > 0 ? childResponses : null);
    }

    private static TagResponse ToTagResponse(Tag t) =>
        new(t.TagId, t.Name, t.Slug, t.PostCount, t.CreatedAt);
}