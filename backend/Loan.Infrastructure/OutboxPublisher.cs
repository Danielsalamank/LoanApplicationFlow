using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Loan.Infrastructure;

/// <summary>
/// Background worker: polls the outbox and pushes pending events to the external
/// service. It runs outside the HTTP request that answers the form.
/// Retry is "at least once" via re-polling; the external service is idempotent by SSN.
/// </summary>
public class OutboxPublisher : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private const int MaxAttempts = 5;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OutboxPublisher> _logger;

    public OutboxPublisher(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory, ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox publishing loop failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task PublishPendingAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LoanDbContext>();

        var pending = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Attempts < MaxAttempts)
            .OrderBy(m => m.OccurredAt)
            .Take(20)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        var http = _httpClientFactory.CreateClient("external-service");

        foreach (var message in pending)
        {
            message.Attempts++;
            try
            {
                var payload = JsonSerializer.Deserialize<JsonElement>(message.Payload);
                var isReturning = payload.GetProperty("isReturningCustomer").GetBoolean();
                var ssn = payload.GetProperty("customer").GetProperty("ssn").GetString();

                var response = isReturning
                    ? await http.PutAsync($"/customers/{ssn}", JsonContent.Create(payload), ct)
                    : await http.PostAsync("/customers", JsonContent.Create(payload), ct);

                response.EnsureSuccessStatusCode();

                message.ProcessedAt = DateTime.UtcNow;
                message.LastError = null;
                _logger.LogInformation("Outbox message {Id} delivered to external service", message.Id);
            }
            catch (Exception ex)
            {
                message.LastError = ex.Message;
                _logger.LogWarning(ex, "Outbox message {Id} delivery failed (attempt {Attempts})", message.Id, message.Attempts);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
