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
using System.Text;
using CDRS.Web.GraphQL;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter()));

// Azure Key Vault（Only enable in Testing environment）
if (!builder.Environment.IsEnvironment("Testing"))
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
        Console.WriteLine($"Key Vault connection failed: {ex.Message}");
    }
}

// JWT Settings
var jwtSettings = builder.Configuration
    .GetSection("JwtSettings")
    .Get<JwtSettings>()!;
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<TokenService>();

// Add services to the container.
builder.Services.AddControllersWithViews();

// Health checks — includes DB connectivity check
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

// Repository & Service (DIP in action)
builder.Services.AddScoped<IDailyReportRepository, DailyReportRepository>();
builder.Services.AddScoped<IDailyReportService, DailyReportService>();

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

// Auto-apply migrations on startup in development environment only.
// In production, migrations are executed as a dedicated CI/CD pipeline step
// before deployment to prevent race conditions with multiple app instances.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
    }
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