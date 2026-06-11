using System.Text.Json;
using System.Text.Json.Serialization;
using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;
using Anthology.Modules.Catalog;
using Anthology.Modules.Identity;
using Anthology.Modules.Profile;
using Anthology.Modules.Admin;
using Anthology.Modules.Recommendations;
using Anthology.Modules.Tracking;
using Anthology.Workers;
using FluentValidation;
using Npgsql;
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

// Shared connection for write-path DbContexts (event store + module projections)
builder.Services.AddScoped(_ =>
    new NpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<EventStoreDbContext>((sp, options) =>
    options.UseNpgsql(sp.GetRequiredService<NpgsqlConnection>())
        .UseSnakeCaseNamingConvention());

var registry = new EventRegistry();
TrackingModule.RegisterEvents(registry);
builder.Services.AddSingleton(registry);

var serializer = new EventSerializer(registry);
builder.Services.AddSingleton(serializer);

var evolverRegistry = new StreamEvolverRegistry();
TrackingModule.RegisterEvolvers(evolverRegistry, serializer);
builder.Services.AddSingleton(evolverRegistry);

builder.Services.AddScoped<StreamRebuilder>();
builder.Services.AddScoped<EventStore>();
builder.Services.AddScoped<OutboxWriter>();
builder.Services.AddSingleton<IntegrationEventTranslator>(sp =>
{
    var translator = new IntegrationEventTranslator();
    TrackingContracts.RegisterTranslators(translator);
    return translator;
});

// Auth
builder.Services.AddAuthorization();

// Modules
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddTrackingModule(builder.Configuration);
builder.Services.AddProfileModule(builder.Configuration);
builder.Services.AddRecommendationsModule(builder.Configuration);
builder.Services.AddScoped<InlineProjector>();

builder.Services.AddHostedService<RebuildJobHost>();

builder.Services.AddSingleton<NpgsqlDataSource>(sp =>
    NpgsqlDataSource.Create(sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!));
builder.Services.AddHostedService<AsyncProjectionHost>();
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
builder.Services.Decorate(typeof(ICommandHandler<,>), typeof(TransactionDecorator<,>));

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
    await services.GetRequiredService<EventStoreDbContext>().Database.MigrateAsync();
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
