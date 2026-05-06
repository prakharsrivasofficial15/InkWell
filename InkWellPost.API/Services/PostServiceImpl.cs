using Ganss.Xss;
using InkWell.Shared.DTOs;
using InkWellPost.API.DTOs;
using InkWellPost.API.Interfaces;
using InkWellPost.API.Models;
using Slugify;
using StackExchange.Redis;
using Azure.Messaging.ServiceBus;
using InkWellPost.API.Events;

namespace InkWellPost.API.Services;

public class PostServiceImpl : IPostService
{
    private readonly IPostRepository _postRepo;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _cache;
    private readonly HtmlSanitizer _sanitizer;
    private readonly SlugHelper _slugger;

    private readonly ServiceBusClient _serviceBusClient;

    public PostServiceImpl(
        IPostRepository postRepo,
        IConnectionMultiplexer redis,
        ServiceBusClient serviceBusClient)
    {
        _postRepo = postRepo;
        _redis = redis;
        _cache = redis.GetDatabase();
        _sanitizer = new HtmlSanitizer();
        _slugger = new SlugHelper();
        _serviceBusClient = serviceBusClient;
    }

    public async Task<PostResponse> CreatePostAsync(Guid authorId, CreatePostRequest request)
    {
        // sanitize HTML to prevent XSS
        var cleanContent = _sanitizer.Sanitize(request.Content);

        // generate unique slug from title
        var slug = await GenerateUniqueSlugAsync(request.Title);

        var post = new Post
        {
            AuthorId = authorId,
            Title = request.Title,
            Slug = slug,
            Content = cleanContent,
            Excerpt = request.Excerpt,
            FeaturedImageUrl = request.FeaturedImageUrl,
            Status = PostStatus.DRAFT,
            ReadTimeMin = ComputeReadTime(cleanContent)
        };

        await _postRepo.AddAsync(post);
        return ToPostResponse(post);
    }

    public async Task<PostResponse> GetPostByIdAsync(Guid postId)
    {
        var post = await _postRepo.GetByPostIdAsync(postId)
            ?? throw new KeyNotFoundException("Post not found.");
        return ToPostResponse(post);
    }

    public async Task<PostResponse> GetPostBySlugAsync(string slug)
    {
        var post = await _postRepo.GetBySlugAsync(slug)
            ?? throw new KeyNotFoundException("Post not found.");
        return ToPostResponse(post);
    }

    public async Task<IEnumerable<PostSummaryResponse>> GetPostsByAuthorAsync(Guid authorId)
    {
        var posts = await _postRepo.GetByAuthorIdAsync(authorId);
        return posts.Select(ToPostSummary);
    }

    public async Task<IEnumerable<PostSummaryResponse>> GetPublishedPostsAsync()
    {
        try
        {
            // try Redis cache first
            var cacheKey = "feed:published";
            var cached = await _cache.StringGetAsync(cacheKey);
            if (cached.HasValue)
            {
                return System.Text.Json.JsonSerializer
                    .Deserialize<IEnumerable<PostSummaryResponse>>(cached!) ?? [];
            }

            var posts = await _postRepo.GetPublishedOrderedAsync();
            var result = posts.Select(ToPostSummary).ToList();

            // cache for 5 minutes
            await _cache.StringSetAsync(
                cacheKey,
                System.Text.Json.JsonSerializer.Serialize(result),
                TimeSpan.FromMinutes(5));

            return result;
        }
        catch (Exception ex)
        {
            // Redis down — fall back to direct DB query
            Console.WriteLine($"Redis unavailable, falling back to DB: {ex.Message}");
            var posts = await _postRepo.GetPublishedOrderedAsync();
            return posts.Select(ToPostSummary);
        }
    }

    public async Task<IEnumerable<PostSummaryResponse>> SearchPostsAsync(string keyword)
    {
        var posts = await _postRepo.SearchByKeywordAsync(keyword);
        return posts.Select(ToPostSummary);
    }

    public async Task<PostResponse> UpdatePostAsync(Guid postId, Guid authorId, UpdatePostRequest request)
    {
        var post = await GetOwnedPostAsync(postId, authorId);

        post.Title           = request.Title;
        post.Content         = _sanitizer.Sanitize(request.Content);
        post.Excerpt         = request.Excerpt;
        post.FeaturedImageUrl = request.FeaturedImageUrl;
        post.ReadTimeMin     = ComputeReadTime(post.Content);

        await _postRepo.UpdateAsync(post);
        await InvalidateFeedCacheAsync();
        return ToPostResponse(post);
    }

    public async Task<PostResponse> PublishPostAsync(Guid postId, Guid authorId, string userRole)
    {
        var post = await GetPostWithAuthCheckAsync(postId, authorId, userRole);

        post.Status = PostStatus.PUBLISHED;
        post.PublishedAt = DateTime.UtcNow;

        await _postRepo.UpdateAsync(post);
        await InvalidateFeedCacheAsync();

        // publish event to Azure Service Bus
        await PublishPostPublishedEventAsync(post);

        return ToPostResponse(post);
    }

    public async Task<PostResponse> UnpublishPostAsync(Guid postId, Guid authorId, string userRole)
    {
        var post = await GetPostWithAuthCheckAsync(postId, authorId, userRole);
        post.Status = PostStatus.UNPUBLISHED;
        await _postRepo.UpdateAsync(post);
        await InvalidateFeedCacheAsync();
        return ToPostResponse(post);
    }

    public async Task DeletePostAsync(Guid postId, Guid authorId, string userRole)
    {
        var post = await GetPostWithAuthCheckAsync(postId, authorId, userRole);
        await _postRepo.DeleteAsync(post.PostId);
        await InvalidateFeedCacheAsync();
    }

    public async Task IncrementViewsAsync(Guid postId, string sessionId)
    {
        try
        {
            // only count once per session using Redis SET
            var sessionKey = $"view:{postId}:{sessionId}";
            bool isNew = await _cache.StringSetAsync(
                sessionKey, "1",
                TimeSpan.FromHours(24),
                When.NotExists);

            if (!isNew) return;
        }
        catch (Exception ex)
        {
            // Redis down — fall back to direct DB query
            Console.WriteLine($"Redis unavailable, falling back to DB: {ex.Message}");
        }

        var post = await _postRepo.GetByPostIdAsync(postId);
        if (post is null) return;

        post.ViewCount++;
        await _postRepo.UpdateAsync(post);
    }

    public async Task LikePostAsync(Guid postId, Guid userId)
    {
        var existing = await _postRepo.GetLikeAsync(postId, userId);
        if (existing is not null)
            throw new InvalidOperationException("Already liked.");

        var like = new PostLike { PostId = postId, UserId = userId };
        await _postRepo.AddLikeAsync(like);

        // atomic increment to prevent race condition
        await _postRepo.IncrementLikesCountAsync(postId);
    }

    public async Task UnlikePostAsync(Guid postId, Guid userId)
    {
        var existing = await _postRepo.GetLikeAsync(postId, userId)
            ?? throw new InvalidOperationException("Not liked yet.");

        await _postRepo.RemoveLikeAsync(existing);

        // atomic decrement to prevent race condition
        await _postRepo.DecrementLikesCountAsync(postId);
    }

    public async Task<PostStatsResponse> GetPostStatsAsync(Guid postId)
    {
        var post = await _postRepo.GetByPostIdAsync(postId)
            ?? throw new KeyNotFoundException("Post not found.");
        return new PostStatsResponse(post.PostId, post.Title, post.ViewCount, post.LikesCount);
    }

    public async Task<int> GetPostCountAsync(Guid authorId) =>
        await _postRepo.CountByAuthorIdAsync(authorId);

    // Private Helpers
    private async Task<Post> GetOwnedPostAsync(Guid postId, Guid authorId)
    {
        var post = await _postRepo.GetByPostIdAsync(postId)
            ?? throw new KeyNotFoundException("Post not found.");

        if (post.AuthorId != authorId)
            throw new UnauthorizedAccessException("You do not own this post.");

        return post;
    }

    private async Task<Post> GetPostWithAuthCheckAsync(Guid postId, Guid authorId, string userRole)
    {
        var post = await _postRepo.GetByPostIdAsync(postId)
            ?? throw new KeyNotFoundException("Post not found.");

        // ADMIN can perform any action, AUTHOR can only act on own posts
        if (userRole != "ADMIN" && post.AuthorId != authorId)
            throw new UnauthorizedAccessException("You do not own this post.");

        return post;
    }

    private async Task<string> GenerateUniqueSlugAsync(string title)
    {
        var baseSlug = _slugger.GenerateSlug(title);
        var slug = baseSlug;
        var counter = 1;

        while (await _postRepo.SlugExistsAsync(slug))
            slug = $"{baseSlug}-{counter++}";

        return slug;
    }

    // 200 words per minute average reading speed
    private static int ComputeReadTime(string content)
    {
        var wordCount = content.Split(' ',
            StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, (int)Math.Ceiling(wordCount / 200.0));
    }

    // private async Task InvalidateFeedCacheAsync() =>
    //     await _cache.KeyDeleteAsync("feed:published");
    private async Task InvalidateFeedCacheAsync()
    {
        try
        {
            await _cache.KeyDeleteAsync("feed:published");
        }
        catch (Exception ex)
        {
            // Redis failure should not block post operations
            Console.WriteLine($"Redis cache invalidation failed: {ex.Message}");
        }
    }

    private static PostResponse ToPostResponse(Post p) =>
        new(p.PostId, p.AuthorId, p.Title, p.Slug, p.Content,
            p.Excerpt, p.FeaturedImageUrl, p.Status.ToString(),
            p.ReadTimeMin, p.ViewCount, p.LikesCount,
            p.CreatedAt, p.PublishedAt);

    private static PostSummaryResponse ToPostSummary(Post p) =>
        new(p.PostId, p.AuthorId, p.Title, p.Slug,
            p.Excerpt, p.FeaturedImageUrl, p.Status.ToString(),
            p.ReadTimeMin, p.ViewCount, p.LikesCount, p.PublishedAt);

    private async Task PublishPostPublishedEventAsync(Post post)
    {
        try
        {
            var sender = _serviceBusClient.CreateSender("inkwell-post-published");

            var evt = new PostPublishedEvent(
                post.PostId,
                post.AuthorId,
                post.Title,
                post.Slug,
                post.Excerpt,
                post.PublishedAt ?? DateTime.UtcNow);

            var message = new ServiceBusMessage(
                BinaryData.FromString(
                    System.Text.Json.JsonSerializer.Serialize(evt)))
            {
                Subject = "post.published"
            };

            await sender.SendMessageAsync(message);
            Console.WriteLine($"Service Bus: post.published sent for {post.Title}");
        }
        catch (Exception ex)
        {
            // don't fail publish if Service Bus is down
            Console.WriteLine($"Service Bus publish failed: {ex.Message}");
        }
    }
}