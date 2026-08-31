using Serilog;
using Serilog.Events;
using CDRS.Application.Interfaces;
using CDRS.Application.Services;
using CDRS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CDRS.Web.Middleware;
using CDRS.Web.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using CDRS.Web.GraphQL;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Azure.Identity;
using CDRS.Web.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()),
    writeToProviders: true);

// Azure Application Insights
builder.Services.AddApplicationInsightsTelemetry();

// Azure Key Vault — production secret source. Skipped in Development and
// Testing so the app runs locally with no Azure credentials (and without a
// DefaultAzureCredential probe on every start).
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
{
    try
    {
        var keyVaultUri = new Uri("https://cdrs-kv-hansl.vault.azure.net/");
        builder.Configuration.AddAzureKeyVault(
            keyVaultUri,
            new DefaultAzureCredential());
    }
    catch (Exception ex)
    {
        // Startup-time failures here are captured by Azure App Service's platform-level
        // diagnostics and Key Vault's own Diagnostic Settings (Audit Logs in Log Analytics)
        // — no application-level logger (Serilog/ILogger) is available yet at this point
        // in the pipeline, since builder.Build() hasn't run.
        //
        // Business decision: fail open — fall back to appsettings/environment configuration
        // rather than crash the application. This trades strict security posture for
        // availability, which is acceptable for this POC. In a real production system,
        // this would more likely fail closed instead, since a silent fallback to
        // potentially stale configuration could mask a real security incident (e.g.
        // Managed Identity permissions being revoked).
        Console.Error.WriteLine($"[WARNING] Key Vault connection failed: {ex.Message}");
    }
}

// JWT Settings
var jwtSettings = builder.Configuration
    .GetSection("JwtSettings")
    .Get<JwtSettings>()!;

// A signing key is mandatory. Outside Development a missing key is a fatal
// misconfiguration (e.g. Key Vault unavailable) and the app must not start.
// In Development, generate an ephemeral key so `dotnet run` works with no
// configuration; tokens simply do not survive a restart.
if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException(
            "JwtSettings:SecretKey is not configured. Provide it via configuration or Key Vault.");

    jwtSettings.SecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    Console.WriteLine("[dev] No JwtSettings:SecretKey configured - generated an ephemeral key for this run.");
}

builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<TokenService>();

// CORS — load allowed origins from configuration
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

// CORS — allow requests from known frontend origins
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        }
        else
        {
            // No origins configured — reject all cross-origin requests
            policy.SetIsOriginAllowed(_ => false);
        }
    });
});

// Add services to the container.
builder.Services.AddControllersWithViews();

// Health checks — includes DB connectivity check
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// In Development with no connection string configured, fall back to a local
// SQLite file so the app runs with zero setup. A real relational provider -
// unlike the in-memory provider - so behaviour matches SQL Server closely.
// Any other environment, and Development once a connection string is set,
// use SQL Server.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var useSqliteDevDb = builder.Environment.IsDevelopment()
    && string.IsNullOrWhiteSpace(connectionString);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (useSqliteDevDb)
    {
        var dbPath = System.IO.Path.Combine(builder.Environment.ContentRootPath, "cdrs-dev.db");
        options.UseSqlite($"Data Source={dbPath}");
    }
    else
    {
        options.UseSqlServer(
            connectionString,
            sqlOptions => sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null));
    }
});

// Repository & Service (DIP in action)
builder.Services.AddScoped<IDailyReportRepository, DailyReportRepository>();
builder.Services.AddScoped<IDailyReportService, DailyReportService>();
// Background Services
builder.Services.AddHostedService<StaleReportDetectionService>();

// JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
    };
});

builder.Services.AddAuthorization();

// GraphQL
builder.Services
    .AddGraphQLServer()
    .AddAuthorization()
    .AddQueryType<ReportQuery>()
    .AddMutationType<ReportMutation>();

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name
                ?? context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers["Retry-After"] = "60";
        await context.HttpContext.Response.WriteAsync(
            "Too many requests. Please try again later.", token);
    };
});

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = false;
    options.ReportApiVersions = true;
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Construction Daily Report API",
        Version = "v1 (Deprecated)",
        Description = "V1 is deprecated. Please migrate to v2."
    });

    c.SwaggerDoc("v2", new()
    {
        Title = "Construction Daily Report API",
        Version = "v2",
        Description = "Status field is now a string. StatusCode field added."
    });

    // JWT Bearer 設定
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter JWT token. Get one from POST /api/auth/token"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
});

// Prepare the database on startup in Development only. In production,
// migrations run as a dedicated CI/CD step before deployment to avoid
// race conditions across multiple app instances.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (useSqliteDevDb)
    {
        // Migrations target SQL Server; build the SQLite dev schema from the model.
        db.Database.EnsureCreated();
    }
    else if (db.Database.IsRelational())
    {
        db.Database.Migrate();
    }

    DevelopmentDataSeeder.Seed(db);
}

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CDRS API v1 (Deprecated)");
    c.SwaggerEndpoint("/swagger/v2/swagger.json", "CDRS API v2");
});
//}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseCors("AllowFrontend");  // must be after UseRouting and before UseAuthentication

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Liveness: is the app running? (used by Azure App Service health monitoring)
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // 不跑任何 check，只確認應用程式活著
});

// Readiness: are all dependencies healthy? (used by monitoring systems)
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = _ => true,  // 跑所有 check，包含 DB
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapGraphQL();

app.Run();

public partial class Program { }
