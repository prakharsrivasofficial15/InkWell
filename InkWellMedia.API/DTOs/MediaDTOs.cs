namespace InkWellMedia.API.DTOs;

// Responses

public record MediaResponse(
    Guid MediaId,
    Guid UploaderId,
    string OriginalName,
    string Filename,
    string Url,
    string MimeType,
    long SizeKb,
    string? AltText,
    Guid? LinkedPostId,
    DateTime UploadedAt
);

public record UpdateAltTextRequest(
    string AltText
);

public record LinkPostRequest(
    Guid PostId
);