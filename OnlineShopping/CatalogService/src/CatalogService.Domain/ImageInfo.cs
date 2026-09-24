namespace CatalogService.Domain;

public sealed class ImageInfo
{
    public ImageInfo(string url, string? altText)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Image url is required.", nameof(url));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Image url must be absolute.", nameof(url));
        }

        Url = url;
        AltText = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim();
    }

    public string Url { get; }

    public string? AltText { get; }
}
