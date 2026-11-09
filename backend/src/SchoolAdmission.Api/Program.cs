using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Common.Errors;
using SchoolAdmission.Api.Configuration;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Features.Classes;

var builder = WebApplication.CreateBuilder(args);

// ---------- Configuration (all overridable with environment variables) ----------
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string is missing. Set the 'ConnectionStrings__Default' environment variable.");
}

builder.Services
    .AddOptions<HoldOptions>()
    .Configure<IConfiguration>((options, config) =>
    {
        options.DurationSeconds = config.GetValue("HOLD_DURATION_SECONDS", HoldOptions.DefaultDurationSeconds);
        options.SweepIntervalSeconds = config.GetValue("HOLD_EXPIRY_SWEEP_SECONDS", HoldOptions.DefaultSweepIntervalSeconds);
    })
    .Validate(o => o.DurationSeconds > 0, "HOLD_DURATION_SECONDS must be a positive whole number.")
    .Validate(o => o.SweepIntervalSeconds > 0, "HOLD_EXPIRY_SWEEP_SECONDS must be a positive whole number.")
    .ValidateOnStart();

// ---------- Services ----------
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<ClassService>();

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = ValidationProblemFactory.Create);

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier));
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

var allowedOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length == 0 || allowedOrigins.Contains("*"))
    {
        policy.AllowAnyOrigin();
    }
    else
    {
        policy.WithOrigins(allowedOrigins);
    }

    policy.AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

// ---------- Database ----------
await DatabaseMigrator.MigrateAsync(app.Services, app.Lifetime.ApplicationStopping);

// ---------- HTTP pipeline ----------
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>Exposed so integration tests can use <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program
{
}
