using InkWell.Shared.DTOs;
using InkWellCategory.API.DTOs;
using InkWellCategory.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InkWellCategory.API.Controllers;

[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoryController(ICategoryService categoryService) =>
        _categoryService = categoryService;

    // Public Endpoints

    // Get all root categories with nested children
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CategoryResponse>>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var categories = await _categoryService.GetAllCategoriesAsync();
        return Ok(new ApiResponse<IEnumerable<CategoryResponse>>(
            true, "Categories fetched.", categories));
    }

    // Get a category by slug
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponse>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        try
        {
            var category = await _categoryService.GetCategoryBySlugAsync(slug);
            return Ok(new ApiResponse<CategoryResponse>(true, "Category fetched.", category));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Get categories assigned to a specific post
    [HttpGet("post/{postId}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CategorySummaryResponse>>), 200)]
    public async Task<IActionResult> GetByPost(Guid postId)
    {
        var categories = await _categoryService.GetCategoriesByPostAsync(postId);
        return Ok(new ApiResponse<IEnumerable<CategorySummaryResponse>>(
            true, "Post categories fetched.", categories));
    }

    // Admin Endpoints

    // Creates a new category (supports parent/child hierarchy)
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponse>), 201)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request)
    {
        try
        {
            var category = await _categoryService.CreateCategoryAsync(request);
            return CreatedAtAction(nameof(GetBySlug),
                new { slug = category.Slug },
                new ApiResponse<CategoryResponse>(true, "Category created.", category));
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Update a category (Admin only)
    [HttpPut("{categoryId}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<CategoryResponse>), 200)]
    public async Task<IActionResult> Update(Guid categoryId, [FromBody] UpdateCategoryRequest request)
    {
        try
        {
            var category = await _categoryService.UpdateCategoryAsync(categoryId, request);
            return Ok(new ApiResponse<CategoryResponse>(true, "Category updated.", category));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Delete a category (Admin only)
    [HttpDelete("{categoryId}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> Delete(Guid categoryId)
    {
        await _categoryService.DeleteCategoryAsync(categoryId);
        return Ok(new ApiResponse<object>(true, "Category deleted.", null));
    }

    // Assign a category to a post (AUTHOR + ADMIN)
    [HttpPost("post/{postId}/assign/{categoryId}")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> AssignToPost(Guid postId, Guid categoryId)
    {
        try
        {
            await _categoryService.AssignCategoryToPostAsync(postId, categoryId);
            return Ok(new ApiResponse<object>(true, "Category assigned to post.", null));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Remove a category from a post (AUTHOR + ADMIN)
    [HttpDelete("post/{postId}/remove/{categoryId}")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> RemoveFromPost(Guid postId, Guid categoryId)
    {
        await _categoryService.RemoveCategoryFromPostAsync(postId, categoryId);
        return Ok(new ApiResponse<object>(true, "Category removed from post.", null));
    }
}