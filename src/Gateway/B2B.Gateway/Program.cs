using System.Text;
using System.Threading.RateLimiting;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Infrastructure.Http;

// ── Rate limiter configuration constants ──────────────────────────────────────
// Tuned for a B2B API: burst-tolerant but bounded to prevent abuse.
// Fixed window: 100 req / 10 s guards against short bursts.
// Sliding window: 500 req / 60 s (6 segments) governs sustained throughput.
const string FixedWindowPolicyName   = "fixed";
const string SlidingWindowPolicyName = "sliding-per-ip";
const string AllowAllCorsPolicy      = "AllowAll";

const int  FixedWindowPermitLimit          = 100;
const int  FixedWindowQueueLimit           = 50;
const int  SlidingWindowPermitLimit        = 500;
const int  SlidingWindowSegmentsPerWindow  = 6;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.Seq(ctx.Configuration["Seq:Url"]!)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "B2B.Gateway"));

// YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// JWT Auth (gateway validates tokens before routing)
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// CorrelationId — gateway is the entry point; it stamps X-Correlation-ID on
// every request so all downstream services share the same trace identifier.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();

// Rate Limiting — partitioned per client IP so one client cannot exhaust
// the global budget and starve legitimate callers.
builder.Services.AddRateLimiter(opt =>
{
    // Short burst window — per IP
    opt.AddFixedWindowLimiter(FixedWindowPolicyName, limiter =>
    {
        limiter.Window = TimeSpan.FromSeconds(10);
        limiter.PermitLimit = FixedWindowPermitLimit;
        limiter.QueueLimit = FixedWindowQueueLimit;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Sustained throughput — partitioned per remote IP (handles proxies via X-Forwarded-For)
    opt.AddPolicy(SlidingWindowPolicyName, httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString()
                 ?? httpContext.Request.Headers["X-Forwarded-For"].ToString()
                 ?? "unknown";

        return RateLimitPartition.GetSlidingWindowLimiter(ip, _ => new SlidingWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = SlidingWindowPermitLimit,
            SegmentsPerWindow = SlidingWindowSegmentsPerWindow,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// CORS
builder.Services.AddCors(opt =>
{
    opt.AddPolicy(AllowAllCorsPolicy, policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("B2B.Gateway"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(
            builder.Configuration["OpenTelemetry:Endpoint"]!)));

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("gateway", () => HealthCheckResult.Healthy("Gateway is running"));

builder.Services.AddHealthChecksUI()
    .AddInMemoryStorage();

var app = builder.Build();

app.UseSerilogRequestLogging();

// CorrelationId before CORS/Rate-limiting so the ID is available in all
// downstream middleware and YARP transform pipelines.
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseCors(AllowAllCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Health Check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecksUI(opt => opt.UIPath = "/health-ui");

app.MapReverseProxy();

await app.RunAsync();
