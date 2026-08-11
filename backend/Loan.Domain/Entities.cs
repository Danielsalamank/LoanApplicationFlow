namespace Loan.Domain;

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Address { get; set; }
    public required string State { get; set; }
    public required string CompanyName { get; set; }
    public required string Ssn { get; set; }
    public List<Application> Applications { get; set; } = new();
}

public class Application
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public decimal RequestedAmount { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
}
