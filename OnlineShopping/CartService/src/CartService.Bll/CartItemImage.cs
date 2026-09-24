namespace CartService.Bll;

public sealed class CartItemImage
{
    public CartItemImage(Uri url, string? altText)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (!url.IsAbsoluteUri)
        {
            throw new ArgumentException("Image url must be an absolute URL.", nameof(url));
        }

        Url = url;
        AltText = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim();
    }

    public Uri Url { get; }

    public string? AltText { get; }

    public CartItemImage Copy() => new CartItemImage(Url, AltText);
}
