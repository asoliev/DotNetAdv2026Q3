using Asp.Versioning.ApiExplorer;
using CatalogService.Api;
using CatalogService.Application;
using CatalogService.Domain;
using CatalogService.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);
var databasePath = builder.Configuration["Database:Path"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "catalog.db");

builder.Services.AddSingleton<ICategoryRepository>(_ => new SqliteCategoryRepository(databasePath));
builder.Services.AddSingleton<IProductRepository>(_ => new SqliteProductRepository(databasePath));
builder.Services.AddSingleton<CategoryService>();
builder.Services.AddSingleton<ProductService>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState));
    });

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
builder.Services.AddSwaggerGen(options =>
{
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "CatalogService.Api.xml"));
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "CatalogService.Application.xml"), true);
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "CatalogService.Domain.xml"), true);
});

var app = builder.Build();
app.UseExceptionHandler();
var versionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    foreach (var description in versionProvider.ApiVersionDescriptions)
    {
        options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
    }
});

app.MapControllers();

app.Run();

/// <summary>Entry point for the Catalog Service API.</summary>
public partial class Program { }
