using Loan.Application;
using Loan.Domain;
using Loan.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<LoanDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=loan.db"));

// Rule engine: every registered IDenyRule is evaluated. Adding a rule = one line here.
builder.Services.AddScoped<IDenyRule, NyStateDenyRule>();
builder.Services.AddScoped<IDenyRule>(_ =>
    new BlacklistedSsnDenyRule(builder.Configuration.GetSection("BlacklistedSsns").Get<string[]>() ?? []));
builder.Services.AddScoped<RuleEngine>();

builder.Services.AddScoped<ILoanStore, LoanStore>();
builder.Services.AddScoped<SubmitApplication>();

builder.Services.AddHttpClient("external-service", client =>
    client.BaseAddress = new Uri(builder.Configuration["ExternalService:BaseUrl"] ?? "http://localhost:4000"));
builder.Services.AddHostedService<OutboxPublisher>();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration["Frontend:Origin"] ?? "http://localhost:3000")
          .AllowAnyHeader()
          .AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<LoanDbContext>().Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.MapControllers();

app.Run();

public partial class Program;
