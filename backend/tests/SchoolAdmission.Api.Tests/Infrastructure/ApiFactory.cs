using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace SchoolAdmission.Api.Tests.Infrastructure;

/// <summary>
/// Runs the real API in memory against a real PostgreSQL database.
/// <para>
/// By default a throw-away PostgreSQL container is started with Testcontainers (Docker
/// must be running). Set <c>TEST_DB_CONNECTION</c> to use an existing database instead.
/// </para>
/// The clock is a <see cref="FakeTimeProvider"/>, so tests can jump forward in time
/// (e.g. to expire a hold) without waiting.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const int HoldDurationSeconds = 600;

    private PostgreSqlContainer? _container;

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 1, 4, 0, 0, TimeSpan.Zero));

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .Build();
            await _container.StartAsync();
            connectionString = _container.GetConnectionString();
        }

        // Program.cs reads configuration while building the app, so environment variables
        // are the most reliable way to pass settings in.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", connectionString);
        Environment.SetEnvironmentVariable("HOLD_DURATION_SECONDS", HoldDurationSeconds.ToString());
        Environment.SetEnvironmentVariable("HOLD_EXPIRY_SWEEP_SECONDS", "1");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
