using Playr.Domain.Posts;

namespace Playr.Application.Posts;

public static class PostMediaValidator
{
    private const long MaxImageBytes = 10 * 1024 * 1024;
    private const long MaxVideoBytes = 100 * 1024 * 1024;

    private static readonly Dictionary<string, PostMediaType> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = PostMediaType.Image,
        [".jpeg"] = PostMediaType.Image,
        [".png"] = PostMediaType.Image,
        [".webp"] = PostMediaType.Image,
        [".gif"] = PostMediaType.Image,
    };

    private static readonly Dictionary<string, PostMediaType> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = PostMediaType.Video,
        [".webm"] = PostMediaType.Video,
        [".mov"] = PostMediaType.Video,
    };

    public const int MaxImageCount = 5;

    public static (PostMediaType MediaType, string Extension) Validate(PostMediaInput media)
    {
        var extension = Path.GetExtension(media.FileName);
        if (string.IsNullOrEmpty(extension))
            throw new InvalidOperationException("Uploaded file must have a valid file extension.");

        if (ImageExtensions.TryGetValue(extension, out var imageType))
        {
            if (media.Length > MaxImageBytes)
                throw new InvalidOperationException("Images cannot be larger than 10 MB.");
            return (imageType, extension.ToLowerInvariant());
        }

        if (VideoExtensions.TryGetValue(extension, out var videoType))
        {
            if (media.Length > MaxVideoBytes)
                throw new InvalidOperationException("Videos cannot be larger than 100 MB.");
            return (videoType, extension.ToLowerInvariant());
        }

        throw new InvalidOperationException("Unsupported file type. Allowed: jpg, jpeg, png, webp, gif, mp4, webm, mov.");
    }

    public static IReadOnlyList<(PostMediaInput Input, PostMediaType MediaType, string Extension)> ValidateMany(
        IReadOnlyList<PostMediaInput>? mediaItems)
    {
        if (mediaItems is null || mediaItems.Count == 0)
            return [];

        if (mediaItems.Count > MaxImageCount)
            throw new InvalidOperationException($"A post can have at most {MaxImageCount} media files.");

        var validated = mediaItems.Select(m => (Input: m, Result: Validate(m)))
            .Select(x => (x.Input, x.Result.MediaType, x.Result.Extension))
            .ToList();

        var hasVideo = validated.Any(v => v.MediaType == PostMediaType.Video);
        if (hasVideo && validated.Count > 1)
            throw new InvalidOperationException("A post can only contain a single video, or up to 5 images, not both.");

        return validated;
    }
}
