namespace Qyl.ChatKit;

/// <summary>
/// Adapts streamed data (image generation results, citations) into ChatKit types.
/// Override methods to customize how model output maps to thread items.
/// </summary>
public class ResponseStreamConverter
{
    /// <summary>
    /// Expected number of partial image updates. Used to normalize progress to [0, 1].
    /// Null means no progress normalization.
    /// </summary>
    public int? PartialImages { get; init; }

    /// <summary>Convert a base64-encoded image into a URL stored on thread items.</summary>
    public virtual ValueTask<string> Base64ImageToUrlAsync(
        string imageId, string base64Image, int? partialImageIndex = null) =>
        new($"data:image/png;base64,{base64Image}");

    /// <summary>Convert a partial image index into normalized progress [0, 1].</summary>
    public virtual float PartialImageIndexToProgress(int partialImageIndex) =>
        PartialImages is > 0
            ? Math.Min(1f, (float)partialImageIndex / PartialImages.Value)
            : 0f;

    /// <summary>Convert a file citation into an assistant message annotation.</summary>
    public virtual ValueTask<Annotation?> FileCitationToAnnotationAsync(
        string? filename, int? index)
    {
        if (filename is null)
            return new((Annotation?)null);

        return new(new Annotation
        {
            Source = new FileSource { Filename = filename, Title = filename },
            Index = index,
        });
    }

    /// <summary>Convert a URL citation into an assistant message annotation.</summary>
    public virtual ValueTask<Annotation?> UrlCitationToAnnotationAsync(
        string url, string? title, int? index) =>
        new(new Annotation
        {
            Source = new UrlSource { Url = url, Title = title ?? url },
            Index = index,
        });
}
