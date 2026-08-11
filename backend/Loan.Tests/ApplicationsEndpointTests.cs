using System.Net.Http.Json;
using Loan.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Loan.Tests;

public class ApplicationsEndpointTests : IClassFixture<TestApi>
{
    private readonly TestApi _api;

    public ApplicationsEndpointTests(TestApi api) => _api = api;

    private static object Payload(string state = "TX", string ssn = "777-77-7777", decimal amount = 15_000m) => new
    {
        firstName = "Ana",
        lastName = "Lopez",
        address = "1 Main St",
        state,
        companyName = "Acme LLC",
        requestedAmount = amount,
        ssn,
    };

    [Fact]
    public async Task Approves_and_persists_a_valid_application()
    {
        var client = _api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/applications", Payload(ssn: "777-77-7777"));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ResponseBody>();
        Assert.Equal("approved", body!.Status);
        Assert.False(body.ReturningCustomer);

        using var db = _api.NewDbContext();
        Assert.True(await db.Customers.AnyAsync(c => c.Ssn == "777-77-7777"));
    }

    [Fact]
    public async Task Denies_new_york_applications()
    {
        var client = _api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/applications", Payload(state: "NY", ssn: "888-88-8888"));

        var body = await response.Content.ReadFromJsonAsync<ResponseBody>();
        Assert.Equal("denied", body!.Status);
        Assert.Contains("New York", body.Reason);
    }

    [Fact]
    public async Task Denies_blacklisted_ssn()
    {
        var client = _api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/applications", Payload(ssn: "111-11-1111"));

        var body = await response.Content.ReadFromJsonAsync<ResponseBody>();
        Assert.Equal("denied", body!.Status);
    }

    [Fact]
    public async Task Rejects_malformed_payload()
    {
        var client = _api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/applications", new { firstName = "Ana" });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Second_submission_with_same_ssn_updates_the_existing_application()
    {
        var client = _api.CreateClient();

        await client.PostAsJsonAsync("/api/applications", Payload(ssn: "999-99-9999", amount: 10_000m));
        var response = await client.PostAsJsonAsync("/api/applications", Payload(ssn: "999-99-9999", amount: 50_000m));

        var body = await response.Content.ReadFromJsonAsync<ResponseBody>();
        Assert.Equal("approved", body!.Status);
        Assert.True(body.ReturningCustomer);

        using var db = _api.NewDbContext();
        var customer = await db.Customers.Include(c => c.Applications).SingleAsync(c => c.Ssn == "999-99-9999");
        Assert.Single(customer.Applications);
        Assert.Equal(50_000m, customer.Applications[0].RequestedAmount);
    }

    private record ResponseBody(string Status, string? Reason, bool ReturningCustomer);
}

public class TestApi : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public Task InitializeAsync()
    {
        _connection.Open();
        using var db = NewDbContext();
        db.Database.EnsureCreated();
        return Task.CompletedTask;
    }

    public new Task DisposeAsync()
    {
        _connection.Dispose();
        base.Dispose();
        return Task.CompletedTask;
    }

    public LoanDbContext NewDbContext() =>
        new(new DbContextOptionsBuilder<LoanDbContext>().UseSqlite(_connection).Options);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<LoanDbContext>>();
            services.RemoveAll<LoanDbContext>();
            services.AddDbContext<LoanDbContext>(options => options.UseSqlite(_connection));

            // El publicador de la bandeja de salida se prueba aparte; aquí no debe salir a la red.
            services.RemoveAll<IHostedService>();
        });
    }
}
