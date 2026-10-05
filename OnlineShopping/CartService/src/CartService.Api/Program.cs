using Asp.Versioning.ApiExplorer;

using CartService.Api.Messaging;
using CartService.Api.Middleware;
using CartService.Bll;
using CartService.Dal;

using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

using ShoppingAuth;

using ShoppingTelemetry;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddShoppingTelemetry("cart-service");
var databaseDir = builder.Configuration["Database:Directory"] ?? builder.Environment.ContentRootPath;
var databasePath = Path.Combine(databaseDir, "cart.db");

builder.Services.AddSingleton<ICartRepository>(_ => new LiteDbCartRepository(databasePath));
builder.Services.AddSingleton<CartManager>();
builder.Services.AddHostedService<RabbitMqCatalogEventConsumer>();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
    });

builder.Services.AddShoppingJwtAuthentication();

builder.Services.AddApiVersioning(options =>
{
    options.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
    options.ReportApiVersions = true;
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddSwaggerGen(options =>
{
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "CartService.Api.xml"));
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "CartService.Bll.xml"), true);
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Cart Service API", Version = "v1" });
    options.SwaggerDoc("v2", new OpenApiInfo { Title = "Cart Service API", Version = "v2" });
});

WebApplication app = builder.Build();
app.UseTraceIdResponseHeader();
IApiVersionDescriptionProvider versionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    foreach (var groupName in versionProvider.ApiVersionDescriptions.Select(description => description.GroupName))
    {
        options.SwaggerEndpoint($"/swagger/{groupName}/swagger.json", groupName.ToUpperInvariant());
    }
});

app.UseAuthentication();
app.UseMiddleware<AccessTokenLoggingMiddleware>();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync().ConfigureAwait(false);
