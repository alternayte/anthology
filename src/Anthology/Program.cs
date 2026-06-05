using System.Text.Json;
using System.Text.Json.Serialization;
using Anthology.Kernel;
using Anthology.Kernel.EventStore;
using Anthology.Kernel.Messaging;
using Anthology.Modules.Catalog;
using Anthology.Modules.Identity;
using Anthology.Modules.Profile;
using Anthology.Modules.Tracking;
using FluentValidation;
using Npgsql;
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
    options.UseNpgsql(sp.GetRequiredService<NpgsqlConnection>()));

var registry = new EventRegistry();
TrackingModule.RegisterEvents(registry);
builder.Services.AddSingleton(registry);
builder.Services.AddSingleton<EventSerializer>();
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
builder.Services.AddScoped<InlineProjector>();

// Async projection host — registered when async projections exist (M5+)
// Infrastructure lives in Workers/AsyncProjectionHost.cs; wire it with:
//   builder.Services.AddNpgsqlDataSource(connectionString);
//   builder.Services.AddSingleton(sp => sp.GetService<AsyncProjectionRegistry>() ?? new AsyncProjectionRegistry());
//   builder.Services.AddHostedService<AsyncProjectionHost>();

// Command handler scanning + decoration via Scrutor
builder.Services.Scan(s => s.FromAssemblyOf<Program>()
    .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<,>)))
    .AsImplementedInterfaces()
    .WithScopedLifetime());
builder.Services.Decorate(typeof(ICommandHandler<,>), typeof(TransactionDecorator<,>));

var app = builder.Build();

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
}

app.Run();

public partial class Program;
