using System.Text;
using System.Threading.RateLimiting;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using B2B.Shared.Core.Interfaces;
using B2B.Shared.Infrastructure.Http;

// ── Rate limiter configuration constants ──────────────────────────────────────
// Three complementary policies work together:
//   1. "fixed"           — global burst guard (100 req / 10 s), applies to all routes.
//   2. "sliding-per-ip"  — sustained per-IP throughput (500 req / 60 s).
//   3. "per-tenant"      — per-tenant sliding window (1000 req / 60 s); tenants that
//                          share IPs (NAT, enterprise proxies) get a fair share.
//
// Partition key precedence: X-Tenant-ID header → TenantId JWT claim → fallback IP.
const string FixedWindowPolicyName   = "fixed";
const string SlidingWindowPolicyName = "sliding-per-ip";
const string PerTenantPolicyName     = "per-tenant";
const string AllowAllCorsPolicy      = "AllowAll";

const int FixedWindowPermitLimit         = 100;
const int FixedWindowQueueLimit          = 50;
const int SlidingWindowPermitLimit       = 500;
const int SlidingWindowSegmentsPerWindow = 6;
const int PerTenantPermitLimit           = 1_000;
const int PerTenantSegmentsPerWindow     = 6;

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

    // Per-tenant sliding window — partition by X-Tenant-ID header, then JWT
    // tenant_id claim, then fall back to IP. Tenants behind corporate NAT that
    // share a single IP still get an isolated quota so one tenant cannot starve
    // others.
    opt.AddPolicy(PerTenantPolicyName, httpContext =>
    {
        // Prefer explicit header (set by tenant SDKs / API clients).
        var tenantKey = httpContext.Request.Headers["X-Tenant-ID"].ToString();

        // Fall back to tenant_id JWT claim extracted from the validated token.
        if (string.IsNullOrEmpty(tenantKey))
        {
            tenantKey = httpContext.User.FindFirst("tenant_id")?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";
        }

        return RateLimitPartition.GetSlidingWindowLimiter(tenantKey, _ => new SlidingWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = PerTenantPermitLimit,
            SegmentsPerWindow = PerTenantSegmentsPerWindow,
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

// OpenTelemetry — traces + metrics exported via OTLP to collector
// (collector scrapes Prometheus / forwards to Grafana Cloud / Jaeger)
var otlpEndpoint = builder.Configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("B2B.Gateway"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

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
