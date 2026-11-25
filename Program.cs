using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Pixcii.Components;
using Pixcii.Data;
using Pixcii.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor Interactive Server services.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register MudBlazor UI services.
builder.Services.AddMudServices();

// Configure EF Core (supports Sqlite or Postgres/Supabase via appsettings.json).
var dbProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    if (dbProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
        dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
        dbProvider.Equals("Supabase", StringComparison.OrdinalIgnoreCase))
    {
        var pgConn = builder.Configuration.GetConnectionString("PostgresConnection")
            ?? builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("PostgreSQL connection string is missing.");
        options.UseNpgsql(pgConn);
    }
    else
    {
        var sqliteConn = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=pixcii.db";
        options.UseSqlite(sqliteConn);
    }
});

// Register Pixcii studio state, media engine, FX & data services.
builder.Services.AddScoped<StudioStateService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddSingleton<MediaConversionService>();
builder.Services.AddSingleton<AnimationFxService>();

var app = builder.Build();

// Ensure database schema and seed palettes exist at startup.
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
