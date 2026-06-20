using Azure.Core;
using Azure.Identity;
using Azure.Communication.Email;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
builder.Services.Configure<ThumbnailCacheOptions>(
    builder.Configuration.GetSection(ThumbnailCacheOptions.SectionName));
builder.Services.Configure<AlbumMetadataEnrichmentOptions>(
    builder.Configuration.GetSection(AlbumMetadataEnrichmentOptions.SectionName));
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.Configure<MusicGenOptions>(
    builder.Configuration.GetSection(MusicGenOptions.SectionName));

// Shared managed-identity credential. A single instance is reused for Blob Storage and both
// Cosmos DbContexts so EF Core caches one internal service provider instead of building a new
// one per request (which triggers ManyServiceProvidersCreatedWarning and fails after 20).
var azureCredential = CreateAzureCredential(builder.Configuration);

// Azure Blob Storage (thumbnails + generated music). Local dev uses a connection string (Azurite);
// Azure uses the blob endpoint with managed identity.
builder.Services.AddSingleton(sp =>
{
    var storage = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(storage.ConnectionString))
    {
        return new BlobServiceClient(storage.ConnectionString);
    }

    if (string.IsNullOrWhiteSpace(storage.BlobEndpoint))
    {
        throw new InvalidOperationException("Storage:BlobEndpoint or Storage:ConnectionString must be configured.");
    }

    return new BlobServiceClient(new Uri(storage.BlobEndpoint), azureCredential);
});

// Thumbnails are cached to Azure Blob Storage.
builder.Services.AddHttpClient<IThumbnailCacheService, BlobThumbnailCacheService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHostedService<AlbumMetadataEnrichmentWorker>();

// Add Entity Framework Core (Azure Cosmos DB) for both the catalog and ASP.NET Identity.
var cosmosConnectionString = builder.Configuration["Cosmos:ConnectionString"];
var cosmosEndpoint = builder.Configuration["Cosmos:Endpoint"];
var cosmosDatabase = builder.Configuration["Cosmos:Database"] ?? "musicstore";
var cosmosUseEmulatorWorkarounds = builder.Configuration.GetValue("Cosmos:UseEmulatorWorkarounds", false);

builder.Services.AddDbContext<MusicStoreEntities>(options =>
    ConfigureCosmos(options, cosmosConnectionString, cosmosEndpoint, cosmosDatabase, cosmosUseEmulatorWorkarounds, azureCredential));

// Add Entity Framework - Identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    ConfigureCosmos(options, cosmosConnectionString, cosmosEndpoint, cosmosDatabase, cosmosUseEmulatorWorkarounds, azureCredential));

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

// Tracks asynchronous AI music generation jobs so the browser polls for progress
// instead of holding a multi-minute HTTP request open (avoids the ingress timeout).
builder.Services.AddSingleton<IAiMusicJobStore, AiMusicJobStore>();

// AI music generation is delegated to the ACE-Step music generation service (separate container).
builder.Services.AddHttpClient<IAiMusicCreationService, AceStepMusicCreationService>(client =>
{
    var timeoutSeconds = builder.Configuration.GetValue("MusicGen:TimeoutSeconds", 600);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds <= 0 ? 600 : timeoutSeconds);
});

// Add IHttpContextAccessor (used by ShoppingCart)
builder.Services.AddHttpContextAccessor();

// Email: transactional receipts + abandoned-cart recovery + opt-in marketing.
builder.Services.Configure<EmailOptions>(
    builder.Configuration.GetSection(EmailOptions.SectionName));
builder.Services.Configure<AbandonedCartOptions>(
    builder.Configuration.GetSection(AbandonedCartOptions.SectionName));

var emailOptions = builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
if (emailOptions.UsesAcs)
{
    // Azure Communication Services Email. Local/dev may use a connection string; Azure uses the
    // resource endpoint with the shared managed-identity credential.
    builder.Services.AddSingleton(sp =>
    {
        var options = sp.GetRequiredService<IOptions<EmailOptions>>().Value;
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return new EmailClient(options.ConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return new EmailClient(new Uri(options.Endpoint), azureCredential);
        }

        throw new InvalidOperationException(
            "Email:ConnectionString or Email:Endpoint must be configured when Email:Provider is 'Acs'.");
    });
    builder.Services.AddSingleton<IEmailSender, AcsEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
}

builder.Services.AddSingleton<EmailTemplateService>();
builder.Services.AddScoped<StoreEmailService>();
builder.Services.AddHostedService<AbandonedCartReminderWorker>();

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
        // Ensure Cosmos database and containers exist
        var musicStoreDb = services.GetRequiredService<MusicStoreEntities>();
        await musicStoreDb.Database.EnsureCreatedAsync();

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

static void ConfigureCosmos(
    DbContextOptionsBuilder options,
    string? connectionString,
    string? endpoint,
    string database,
    bool useEmulatorWorkarounds,
    TokenCredential credential)
{
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseCosmos(connectionString, database, cosmos =>
        {
            if (useEmulatorWorkarounds)
            {
                // The local Cosmos emulator (vnext) serves plain HTTP and requires Gateway mode.
                cosmos.ConnectionMode(Microsoft.Azure.Cosmos.ConnectionMode.Gateway);
                cosmos.LimitToEndpoint(true);
            }
        });
    }
    else if (!string.IsNullOrWhiteSpace(endpoint))
    {
        options.UseCosmos(endpoint, credential, database);
    }
    else
    {
        throw new InvalidOperationException("Cosmos:ConnectionString or Cosmos:Endpoint must be configured.");
    }
}

static TokenCredential CreateAzureCredential(IConfiguration configuration)
{
    var clientId = configuration["AZURE_CLIENT_ID"];
    if (!string.IsNullOrWhiteSpace(clientId))
    {
        return new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = clientId });
    }

    return new DefaultAzureCredential();
}
