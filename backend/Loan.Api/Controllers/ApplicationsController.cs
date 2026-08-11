using System.ComponentModel.DataAnnotations;
using Loan.Application;
using Loan.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Loan.Api.Controllers;

public record SubmitApplicationRequest(
    [Required, StringLength(80)] string FirstName,
    [Required, StringLength(80)] string LastName,
    [Required, StringLength(200)] string Address,
    [Required, StringLength(2, MinimumLength = 2)] string State,
    [Required, StringLength(120)] string CompanyName,
    [Range(1, 10_000_000)] decimal RequestedAmount,
    [Required, RegularExpression(@"^\d{3}-?\d{2}-?\d{4}$")] string Ssn);

public record SubmitApplicationResponse(string Status, string? Reason, bool ReturningCustomer);

[ApiController]
[Route("api/applications")]
[Produces("application/json")]
public class ApplicationsController : ControllerBase
{
    private readonly SubmitApplication _submitApplication;

    public ApplicationsController(SubmitApplication submitApplication) => _submitApplication = submitApplication;

    [HttpPost]
    public async Task<ActionResult<SubmitApplicationResponse>> Submit(SubmitApplicationRequest request, CancellationToken ct)
    {
        var data = new LoanApplicationData(
            request.FirstName.Trim(),
            request.LastName.Trim(),
            request.Address.Trim(),
            request.State.Trim().ToUpperInvariant(),
            request.CompanyName.Trim(),
            request.RequestedAmount,
            NormalizeSsn(request.Ssn));

        var result = await _submitApplication.ExecuteAsync(data, ct);

        return Ok(new SubmitApplicationResponse(
            result.Approved ? "approved" : "denied",
            result.DenialReason,
            result.IsReturningCustomer));
    }

    private static string NormalizeSsn(string ssn)
    {
        var digits = new string(ssn.Where(char.IsDigit).ToArray());
        return $"{digits[..3]}-{digits[3..5]}-{digits[5..]}";
    }
}
