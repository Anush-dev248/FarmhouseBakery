using System.Threading.RateLimiting;
using FarmhouseBakery.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FarmhouseBakery.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Records who added/edited/deleted things (Users > Activity).
builder.Services.AddScoped<ActivityLogger>();

// Azure SQL Database. The connection string is NOT in appsettings.json:
// locally it comes from dotnet user-secrets, on Azure from the Web App's settings.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
        // Azure SQL can drop connections briefly; retry instead of failing the request.
        sql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));

// Menu images and resumes are stored in Azure Blob Storage ("AzureStorage" connection string).
builder.Services.AddSingleton<BlobStorageService>();

builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();


// Re-check each login every minute, so admin rights removed on the Users page stop working quickly.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(1);
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // Always HTTPS-only outside development (the local "http" profile still works).
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;

    // "Remember me" (a persistent login) lasts 14 days; a normal login lasts 2 hours.
    options.Events.OnSigningIn = context =>
    {
        if (context.Properties.IsPersistent)
        {
            context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14);
        }

        return Task.CompletedTask;
    };
});

// Max 5 login / register / password attempts per minute from one IP address.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "text/plain";
        await context.HttpContext.Response.WriteAsync(
            "Too many attempts. Please wait a minute and try again.",
            cancellationToken);
    };
});

builder.Services.AddScoped<EmailService>();

var app = builder.Build();

// On Azure App Service, Azure's front end forwards each request. Read the visitor's real
// IP and https from the headers it adds, so the attempt limits apply per visitor.
// (Only on App Service, where WEBSITE_SITE_NAME is set; it takes the last address,
// which Azure adds itself, so visitors can't fake it.)
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")))
{
    var forwardedOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    };
    forwardedOptions.KnownIPNetworks.Clear();
    forwardedOptions.KnownProxies.Clear();

    app.UseForwardedHeaders(forwardedOptions);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Content Security Policy: the browser may only load scripts, styles, fonts, images and
// embedded frames from our own site and the few services the pages use.
var storageHost = "https://farmhousebakerystorage.blob.core.windows.net";
var contentSecurityPolicy = string.Join("; ",
    "default-src 'self'",
    "script-src 'self' 'unsafe-inline'",
    "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdnjs.cloudflare.com",
    "font-src 'self' data: https://fonts.gstatic.com https://cdnjs.cloudflare.com",
    $"img-src 'self' data: {storageHost}",
    "frame-src https://www.google.com https://maps.google.com https://www.youtube-nocookie.com",
    "connect-src 'self'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'");

// Basic security headers on every response.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Content-Security-Policy"] = contentSecurityPolicy;
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    // The website's database login can only read/write data (not change tables),
    // so migrations are applied separately with "dotnet ef database update".
    // Set Database:MigrateOnStartup=true only when the login is allowed to change tables.
    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        await dbContext.Database.MigrateAsync();
    }

    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }
}


app.Run();