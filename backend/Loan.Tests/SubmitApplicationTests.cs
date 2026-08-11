using Loan.Application;
using Loan.Domain;
using Loan.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Loan.Tests;

public class SubmitApplicationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LoanDbContext> _options;

    public SubmitApplicationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<LoanDbContext>().UseSqlite(_connection).Options;

        using var db = new LoanDbContext(_options);
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private LoanDbContext NewContext() => new(_options);

    private SubmitApplication NewUseCase(LoanDbContext db) =>
        new(new RuleEngine([new NyStateDenyRule(), new BlacklistedSsnDenyRule(["111-11-1111"])]), new LoanStore(db));

    private static LoanApplicationData Data(decimal amount = 10_000m, string company = "Acme LLC") =>
        new("Ana", "Lopez", "1 Main St", "TX", company, amount, "555-55-5555");

    [Fact]
    public async Task New_customer_creates_customer_application_and_event()
    {
        using (var db = NewContext())
            await NewUseCase(db).ExecuteAsync(Data(), CancellationToken.None);

        using var assertDb = NewContext();
        Assert.Equal(1, await assertDb.Customers.CountAsync());
        Assert.Equal(1, await assertDb.Applications.CountAsync());

        var message = await assertDb.OutboxMessages.SingleAsync();
        Assert.Contains("\"isReturningCustomer\":false", message.Payload);
        Assert.Null(message.ProcessedAt);
    }

    [Fact]
    public async Task Returning_customer_updates_records_instead_of_duplicating()
    {
        using (var db = NewContext())
            await NewUseCase(db).ExecuteAsync(Data(), CancellationToken.None);

        SubmitResult result;
        using (var db = NewContext())
            result = await NewUseCase(db).ExecuteAsync(Data(amount: 42_000m, company: "Acme Global"), CancellationToken.None);

        Assert.True(result.Approved);
        Assert.True(result.IsReturningCustomer);

        using var assertDb = NewContext();
        var customer = await assertDb.Customers.Include(c => c.Applications).SingleAsync();
        Assert.Equal("Acme Global", customer.CompanyName);
        Assert.Single(customer.Applications);
        Assert.Equal(42_000m, customer.Applications[0].RequestedAmount);
        Assert.Equal(2, await assertDb.OutboxMessages.CountAsync());
        Assert.Contains(await assertDb.OutboxMessages.ToListAsync(), m => m.Payload.Contains("\"isReturningCustomer\":true"));
    }

    [Fact]
    public async Task Denied_application_persists_nothing()
    {
        using (var db = NewContext())
        {
            var result = await NewUseCase(db).ExecuteAsync(
                new LoanApplicationData("Ana", "Lopez", "1 Main St", "NY", "Acme LLC", 10_000m, "555-55-5555"),
                CancellationToken.None);
            Assert.False(result.Approved);
        }

        using var assertDb = NewContext();
        Assert.Equal(0, await assertDb.Customers.CountAsync());
        Assert.Equal(0, await assertDb.Applications.CountAsync());
        Assert.Equal(0, await assertDb.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Failure_while_saving_rolls_back_customer_application_and_event()
    {
        using (var db = NewContext())
            await NewUseCase(db).ExecuteAsync(Data(), CancellationToken.None);

        // Second customer reusing the same SSN through a store that skips the
        // returning-customer lookup: the unique index makes SaveChanges fail.
        using (var db = NewContext())
        {
            var useCase = new SubmitApplication(
                new RuleEngine([]),
                new AlwaysNewCustomerStore(db));

            await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
                useCase.ExecuteAsync(Data(amount: 99_000m), CancellationToken.None));
        }

        using var assertDb = NewContext();
        Assert.Equal(1, await assertDb.Customers.CountAsync());
        Assert.Equal(1, await assertDb.Applications.CountAsync());
        Assert.Equal(1, await assertDb.OutboxMessages.CountAsync());
    }

    private class AlwaysNewCustomerStore(LoanDbContext db) : ILoanStore
    {
        private readonly LoanStore _inner = new(db);

        public Task<Customer?> FindCustomerBySsnAsync(string ssn, CancellationToken ct) =>
            Task.FromResult<Customer?>(null);

        public Task SaveApprovedAsync(Customer customer, Domain.Application application, LoanEvent loanEvent, CancellationToken ct) =>
            _inner.SaveApprovedAsync(customer, application, loanEvent, ct);
    }
}
