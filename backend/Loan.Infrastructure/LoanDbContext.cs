using Microsoft.EntityFrameworkCore;

namespace Loan.Infrastructure;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Payload { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

public class LoanDbContext : DbContext
{
    public LoanDbContext(DbContextOptions<LoanDbContext> options) : base(options) { }

    public DbSet<Domain.Customer> Customers => Set<Domain.Customer>();
    public DbSet<Domain.Application> Applications => Set<Domain.Application>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain.Customer>(e =>
        {
            e.HasIndex(c => c.Ssn).IsUnique();
            e.Property(c => c.Ssn).IsRequired();
        });
        modelBuilder.Entity<Domain.Application>(e =>
        {
            e.HasOne(a => a.Customer)
             .WithMany(c => c.Applications)
             .HasForeignKey(a => a.CustomerId);
            e.Property(a => a.RequestedAmount).HasPrecision(18, 2);
        });
    }
}
