using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MvcMusicStore.Models;
using MvcMusicStore.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient<IAlbumArtworkService, MusicBrainzAlbumArtworkService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<IAlbumMetadataService, MusicBrainzAlbumArtworkService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<IThumbnailCacheService, LocalThumbnailCacheService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.Configure<ThumbnailCacheOptions>(
    builder.Configuration.GetSection(ThumbnailCacheOptions.SectionName));
builder.Services.Configure<AlbumMetadataEnrichmentOptions>(
    builder.Configuration.GetSection(AlbumMetadataEnrichmentOptions.SectionName));
builder.Services.AddHostedService<AlbumMetadataEnrichmentWorker>();

// Add Entity Framework - Music Store
builder.Services.AddDbContext<MusicStoreEntities>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("MusicStoreEntities")));

// Add Entity Framework - Identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Configure cookie authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/LogOff";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Add session support for shopping cart
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.Configure<AiMusicCreationOptions>(
    builder.Configuration.GetSection(AiMusicCreationOptions.SectionName));
builder.Services.AddScoped<IAiMusicCreationService, LocalAiMusicCreationService>();

// Add IHttpContextAccessor (used by ShoppingCart)
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Initialize database and seed data
await SeedDatabaseAsync(app);

app.Run();

static async Task SeedDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    try
    {
        // Ensure databases are created
        var musicStoreDb = services.GetRequiredService<MusicStoreEntities>();
        await musicStoreDb.Database.EnsureCreatedAsync();
        if (!await HasAlbumCatalogColumnsAsync(musicStoreDb))
        {
            await musicStoreDb.Database.EnsureDeletedAsync();
            await musicStoreDb.Database.EnsureCreatedAsync();
        }

        var identityDb = services.GetRequiredService<ApplicationDbContext>();
        await identityDb.Database.EnsureCreatedAsync();

        // Seed admin user
        var configuration = services.GetRequiredService<IConfiguration>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        string adminUsername = configuration["AppSettings:DefaultAdminUsername"] ?? "Administrator";
        string adminPassword = configuration["AppSettings:DefaultAdminPassword"] ?? "YouShouldChangeThisPassword1!";
        const string adminRole = "Administrator";

        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(adminRole));
        }

        var adminUser = await userManager.FindByNameAsync(adminUsername);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser { UserName = adminUsername };
            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, adminRole);
            }
        }

        // Seed music store data
        await SampleData.SeedAsync(musicStoreDb);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

static async Task<bool> HasAlbumCatalogColumnsAsync(MusicStoreEntities dbContext)
{
    await using var connection = dbContext.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
        await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = "PRAGMA table_info('Albums');";

    await using var reader = await command.ExecuteReaderAsync();
    var hasReleaseDate = false;
    var hasIsAvailable = false;
    var hasMetadataThumbnailUrl = false;
    var hasUploadedThumbnailUrl = false;
    while (await reader.ReadAsync())
    {
        var columnName = reader.GetString(reader.GetOrdinal("name"));
        if (string.Equals(columnName, "ReleaseDate", StringComparison.OrdinalIgnoreCase))
        {
            hasReleaseDate = true;
        }
        else if (string.Equals(columnName, "IsAvailable", StringComparison.OrdinalIgnoreCase))
        {
            hasIsAvailable = true;
        }
        else if (string.Equals(columnName, "MetadataThumbnailUrl", StringComparison.OrdinalIgnoreCase))
        {
            hasMetadataThumbnailUrl = true;
        }
        else if (string.Equals(columnName, "UploadedThumbnailUrl", StringComparison.OrdinalIgnoreCase))
        {
            hasUploadedThumbnailUrl = true;
        }
    }

    return hasReleaseDate && hasIsAvailable && hasMetadataThumbnailUrl && hasUploadedThumbnailUrl;
}
