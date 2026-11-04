using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CartService.Api.Tests;

public sealed class CartsApiTests : IClassFixture<CartApiFactory>
{
    private readonly HttpClient _client;

    public CartsApiTests(CartApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetV1_ReturnsCartModel()
    {
        var cartKey = Guid.NewGuid().ToString("N");
        using var addResponse = await AddItemAsync(cartKey);
        addResponse.EnsureSuccessStatusCode();

        using var response = await _client.GetAsync($"/api/v1/carts/{cartKey}");
        var cart = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(cartKey, cart.GetProperty("cartKey").GetString());
        Assert.Single(cart.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task GetV2_ReturnsItemList()
    {
        var cartKey = Guid.NewGuid().ToString("N");
        using var addResponse = await AddItemAsync(cartKey);
        addResponse.EnsureSuccessStatusCode();

        using var response = await _client.GetAsync($"/api/v2/carts/{cartKey}");
        var items = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.Single(items.EnumerateArray());
    }

    [Fact]
    public async Task AddItem_CreatesCartWhenMissing()
    {
        var cartKey = Guid.NewGuid().ToString("N");

        using var response = await AddItemAsync(cartKey);
        var cart = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(cartKey, cart.GetProperty("cartKey").GetString());
        Assert.Single(cart.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task AddItem_WithInvalidPrice_Returns400ProblemDetails()
    {
        var cartKey = Guid.NewGuid().ToString("N");
        using var response = await _client.PostAsJsonAsync($"/api/v1/carts/{cartKey}/items", new
        {
            id = 1,
            name = "Mouse",
            price = -1m,
            quantity = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task DeleteItem_WhenExists_Returns200()
    {
        var cartKey = Guid.NewGuid().ToString("N");
        using var addResponse = await AddItemAsync(cartKey);
        addResponse.EnsureSuccessStatusCode();

        using var response = await _client.DeleteAsync($"/api/v1/carts/{cartKey}/items/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteItem_WhenMissing_Returns404()
    {
        using var response = await _client.DeleteAsync($"/api/v1/carts/{Guid.NewGuid():N}/items/1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/swagger/v2/swagger.json")]
    public async Task SwaggerDocument_IsAvailableForEachApiVersion(string path)
    {
        using var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<HttpResponseMessage> AddItemAsync(string cartKey)
    {
        return _client.PostAsJsonAsync($"/api/v1/carts/{cartKey}/items", new
        {
            id = 1,
            name = "Mouse",
            price = 10m,
            quantity = 1
        });
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }
}
