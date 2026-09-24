using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anthology.Kernel;
using Anthology.Modules.Catalog;
using Anthology.Modules.Identity;
using Anthology.Modules.Profile;
using Anthology.Modules.Admin;
using Anthology.Modules.Recommendations;
using Anthology.Modules.Tracking;
using Anthology.Workers;
using Deedbox;
using FluentValidation;
using Npgsql;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// JSON serialization
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});

// ProblemDetails + global exception handler
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// OpenAPI
builder.Services.AddOpenApi();

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Shared connection for module DbContexts
builder.Services.AddScoped(_ =>
    new NpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));

// Event store: Deedbox owns its schema, streams, projections and background runner
builder.Services.AddDeedbox(deedbox =>
{
    deedbox.UsePostgres(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!)
        .ConfigureJson(o => o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)))
        .AddTracking();
    if (builder.Environment.IsDevelopment())
        deedbox.ApplySchemaOnStartup();
});

// Auth
builder.Services.AddAuthorization();

// Modules
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddTrackingModule(builder.Configuration);
builder.Services.AddProfileModule(builder.Configuration);
builder.Services.AddRecommendationsModule(builder.Configuration);
builder.Services.AddHostedService(sp =>
    new EmbeddingWorker(
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("EmbeddingApi"),
        sp.GetRequiredService<IOptions<EmbeddingOptions>>(),
        sp.GetRequiredService<ILogger<EmbeddingWorker>>()));

// Command handler scanning + decoration via Scrutor
builder.Services.Scan(s => s.FromAssemblyOf<Program>()
    .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<,>)))
    .AsImplementedInterfaces()
    .WithScopedLifetime());
builder.Services.Decorate(typeof(ICommandHandler<,>), typeof(ValidationDecorator<,>));

// Build-time OpenAPI generation runs this entry point; background services and Deedbox's startup check need a database.
if (Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
    builder.Services.RemoveAll<IHostedService>();

var app = builder.Build();

if (args.Length > 0 && args[0] == "seed-catalog")
{
    using var scope = app.Services.CreateScope();
    var sp = scope.ServiceProvider;
    await sp.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();

    var seeder = sp.GetRequiredService<CatalogSeeder>();
    var cmdOptions = ParseSeedArgs(args);
    await seeder.SeedAsync(cmdOptions, CancellationToken.None);
    return;
}

// Middleware
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapIdentityEndpoints();
app.MapCatalogEndpoints();
app.MapTrackingEndpoints();
app.MapProfileEndpoints();
app.MapRecommendationsEndpoints();
app.MapAdminEndpoints();

// SPA fallback
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

// Apply migrations in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    await services.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    await services.GetRequiredService<CatalogDbContext>().Database.MigrateAsync();
    await services.GetRequiredService<TrackingDbContext>().Database.MigrateAsync();
    await services.GetRequiredService<ProfileDbContext>().Database.MigrateAsync();
    await services.GetRequiredService<RecommendationsDbContext>().Database.MigrateAsync();
}

app.Run();

static SeedCommandOptions ParseSeedArgs(string[] args)
{
    var count = 500;
    string[]? providers = null;
    string[] lists = ["popular", "top_rated", "trending"];
    MediaType[]? mediaTypes = null;

    for (var i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--count" when i + 1 < args.Length:
                count = int.Parse(args[++i]);
                break;
            case "--providers" when i + 1 < args.Length:
                providers = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries);
                break;
            case "--lists" when i + 1 < args.Length:
                lists = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries);
                break;
            case "--media-types" when i + 1 < args.Length:
                mediaTypes = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => Enum.Parse<MediaType>(s, ignoreCase: true))
                    .ToArray();
                break;
        }
    }

    return new SeedCommandOptions(count, providers, lists, mediaTypes);
}

public partial class Program;
