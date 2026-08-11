namespace Loan.Domain;

/// <summary>
/// Regla de rechazo. Si una regla devuelve un motivo distinto de null, la solicitud se rechaza.
/// Para agregar una regla nueva: crear una clase que implemente esta interfaz y registrarla en el contenedor.
/// </summary>
public interface IDenyRule
{
    string? Evaluate(LoanApplicationData data);
}

/// <summary>Copia inmutable de los datos del formulario que evalúan las reglas.</summary>
public record LoanApplicationData(
    string FirstName,
    string LastName,
    string Address,
    string State,
    string CompanyName,
    decimal RequestedAmount,
    string Ssn);
