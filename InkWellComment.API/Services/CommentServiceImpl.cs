using Azure.Messaging.ServiceBus;
using InkWellComment.API.DTOs;
using InkWellComment.API.Events;
using InkWellComment.API.Interfaces;
using InkWellComment.API.Models;
using System.Text.Json;

namespace InkWellComment.API.Services;

public class CommentServiceImpl : ICommentService
{
    private readonly ICommentRepository _commentRepo;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly IConfiguration _config;

    public CommentServiceImpl(
        ICommentRepository commentRepo,
        ServiceBusClient serviceBusClient,
        IConfiguration config)
    {
        _commentRepo = commentRepo;
        _serviceBusClient = serviceBusClient;
        _config = config;
    }

    public async Task<CommentResponse> AddCommentAsync(Guid authorId, AddCommentRequest request)
    {
        // enforce two-level threading: replies cannot have their own replies
        if (request.ParentCommentId.HasValue)
        {
            var parent = await _commentRepo.GetByCommentIdAsync(request.ParentCommentId.Value)
                ?? throw new KeyNotFoundException("Parent comment not found.");

            if (parent.ParentCommentId.HasValue)
                throw new InvalidOperationException(
                    "Replies to replies are not allowed. Max two levels.");
        }

        var comment = new Comment
        {
            PostId          = request.PostId,
            PostAuthorId    = request.PostAuthorId,
            AuthorId        = authorId,
            ParentCommentId = request.ParentCommentId,
            Content         = request.Content,
            Status          = CommentStatus.APPROVED
        };

        await _commentRepo.AddAsync(comment);

        // publishes events to Azure Service Bus
        Guid? parentCommentAuthorId = null;
        if (comment.ParentCommentId.HasValue)
        {
            var parent = await _commentRepo.GetByCommentIdAsync(comment.ParentCommentId.Value);
            parentCommentAuthorId = parent?.AuthorId;
        }

        await PublishCommentAddedEventAsync(comment, parentCommentAuthorId);

        return ToCommentResponse(comment, null);
    }

    public async Task<CommentResponse> GetCommentByIdAsync(Guid commentId)
    {
        var comment = await _commentRepo.GetByCommentIdAsync(commentId)
            ?? throw new KeyNotFoundException("Comment not found.");

        var replies = await _commentRepo.GetRepliesAsync(commentId);
        return ToCommentResponse(comment, replies.Select(r => ToCommentResponse(r, null)).ToList());
    }

    public async Task<IEnumerable<CommentResponse>> GetCommentsByPostAsync(Guid postId)
    {
        // gets the top-level comments only
        var topLevel = await _commentRepo.GetTopLevelByPostIdAsync(postId);
        var result = new List<CommentResponse>();

        foreach (var comment in topLevel)
        {
            // attach replies to each top-level comment
            var replies = await _commentRepo.GetRepliesAsync(comment.CommentId);
            result.Add(ToCommentResponse(
                comment,
                replies.Select(r => ToCommentResponse(r, null)).ToList()));
        }

        return result;
    }

    public async Task<IEnumerable<CommentResponse>> GetRepliesAsync(Guid parentCommentId)
    {
        var replies = await _commentRepo.GetRepliesAsync(parentCommentId);
        return replies.Select(r => ToCommentResponse(r, null));
    }

    public async Task<CommentResponse> UpdateCommentAsync(
        Guid commentId, Guid authorId, UpdateCommentRequest request)
    {
        var comment = await GetOwnedCommentAsync(commentId, authorId);
        comment.Content = request.Content;
        await _commentRepo.UpdateAsync(comment);
        return ToCommentResponse(comment, null);
    }

    public async Task DeleteCommentAsync(Guid commentId, Guid authorId)
    {
        var comment = await GetOwnedCommentAsync(commentId, authorId);
        // also deletes all replies
        await _commentRepo.SoftDeleteWithRepliesAsync(comment.CommentId);
    }

    public async Task<CommentResponse> ApproveCommentAsync(Guid commentId)
    {
        var comment = await _commentRepo.GetByCommentIdAsync(commentId)
            ?? throw new KeyNotFoundException("Comment not found.");

        comment.Status = CommentStatus.APPROVED;
        await _commentRepo.UpdateAsync(comment);
        return ToCommentResponse(comment, null);
    }

    public async Task<CommentResponse> RejectCommentAsync(Guid commentId)
    {
        var comment = await _commentRepo.GetByCommentIdAsync(commentId)
            ?? throw new KeyNotFoundException("Comment not found.");

        comment.Status = CommentStatus.REJECTED;
        await _commentRepo.UpdateAsync(comment);
        return ToCommentResponse(comment, null);
    }

    public async Task LikeCommentAsync(Guid commentId, Guid userId)
    {
        var existing = await _commentRepo.GetLikeAsync(commentId, userId);
        if (existing is not null)
            throw new InvalidOperationException("Already liked.");

        var like = new CommentLike { CommentId = commentId, UserId = userId };
        await _commentRepo.AddLikeAsync(like);

        var comment = await _commentRepo.GetByCommentIdAsync(commentId);
        if (comment is null) return;
        comment.LikesCount++;
        await _commentRepo.UpdateAsync(comment);
    }

    public async Task UnlikeCommentAsync(Guid commentId, Guid userId)
    {
        var existing = await _commentRepo.GetLikeAsync(commentId, userId)
            ?? throw new InvalidOperationException("Not liked yet.");

        await _commentRepo.RemoveLikeAsync(existing);

        var comment = await _commentRepo.GetByCommentIdAsync(commentId);
        if (comment is null) return;
        if (comment.LikesCount > 0) comment.LikesCount--;
        await _commentRepo.UpdateAsync(comment);
    }

    public async Task<int> GetCommentCountAsync(Guid postId) =>
        await _commentRepo.CountByPostIdAsync(postId);

    // Private Helpers

    private async Task<Comment> GetOwnedCommentAsync(Guid commentId, Guid authorId)
    {
        var comment = await _commentRepo.GetByCommentIdAsync(commentId)
            ?? throw new KeyNotFoundException("Comment not found.");

        if (comment.AuthorId != authorId)
            throw new UnauthorizedAccessException("You do not own this comment.");

        return comment;
    }

    private async Task PublishCommentAddedEventAsync(Comment comment, Guid? parentCommentAuthorId)
    {
        try
        {
            var sender = _serviceBusClient.CreateSender("inkwell-comment-added");
            var evt = new CommentAddedEvent(
                comment.CommentId,
                comment.PostId,
                comment.AuthorId,
                comment.PostAuthorId,         // now correct
                comment.ParentCommentId,
                parentCommentAuthorId,
                comment.Content,
                comment.CreatedAt);

            var message = new ServiceBusMessage(
                BinaryData.FromString(JsonSerializer.Serialize(evt)))
            {
                Subject = "comment.added"
            };

            await sender.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            // don't fail the comment creation if Service Bus is down
            Console.WriteLine($"Service Bus publish failed: {ex.Message}");
        }
    }

    private static CommentResponse ToCommentResponse(
        Comment c, List<CommentResponse>? replies) =>
        new(c.CommentId, c.PostId, c.AuthorId, c.ParentCommentId,
            c.Content, c.LikesCount, c.Status.ToString(),
            c.CreatedAt, c.UpdatedAt, replies);
}