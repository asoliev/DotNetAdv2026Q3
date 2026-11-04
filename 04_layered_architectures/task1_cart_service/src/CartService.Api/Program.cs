using Asp.Versioning.ApiExplorer;
using CartService.Api;
using CartService.Bll;
using CartService.Dal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);
var databasePath = builder.Configuration["Database:Path"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "cart.db");

builder.Services.AddSingleton<ICartRepository>(_ => new LiteDbCartRepository(databasePath));
builder.Services.AddSingleton<CartService.Bll.CartService>();
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
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "CartService.Api.xml"));
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "CartService.Bll.xml"), true);
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

/// <summary>Entry point for the Cart Service API.</summary>
public partial class Program { }
