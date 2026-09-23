namespace CartService.Bll;

public sealed class CartItemImage
{
    public CartItemImage(string url, string? altText)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Image url is required.", nameof(url));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Image url must be an absolute URL.", nameof(url));
        }

        Url = url;
        AltText = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim();
    }

    public string Url { get; }

    public string? AltText { get; }

    public CartItemImage Copy() => new CartItemImage(Url, AltText);
}
