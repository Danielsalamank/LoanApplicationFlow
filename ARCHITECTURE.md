# Arquitectura

## Estructura del proyecto

```
backend/
  Loan.Domain/          Entidades (Customer, Application) y el contrato IDenyRule. Sin dependencias.
  Loan.Application/     Caso de uso (SubmitApplication), motor de reglas, reglas de rechazo y el puerto ILoanStore.
  Loan.Infrastructure/  DbContext de EF Core, LoanStore (ILoanStore), tabla de bandeja de salida y proceso publicador.
  Loan.Api/             Controlador de ASP.NET, validación de la petición y registro de dependencias.
  Loan.Tests/           Pruebas con xUnit.
frontend/               Next.js con App Router: formulario, /approved y /denied.
mock-service/           Aplicación Express que hace de servicio externo.
```

Las dependencias apuntan hacia adentro: `Api → Infrastructure → Application → Domain`.
La capa de aplicación solo conoce el puerto `ILoanStore`, de modo que EF Core, SQLite y
el cliente HTTP se pueden reemplazar sin tocar las reglas de negocio. El controlador se
limita a traducir HTTP al caso de uso.

## Motor de reglas

`RuleEngine` recibe un `IEnumerable<IDenyRule>` y devuelve el primer motivo de rechazo
que encuentre; si ninguna regla se cumple, la solicitud queda aprobada. El motor no
sabe nada de las reglas concretas.

Para agregar una regla no se modifica ninguna de las existentes:

```csharp
public class MinimumAmountDenyRule : IDenyRule
{
    public string? Evaluate(LoanApplicationData data) =>
        data.RequestedAmount < 1_000m ? "Minimum amount is $1,000." : null;
}
```

Y una línea en `Program.cs`:

```csharp
builder.Services.AddScoped<IDenyRule, MinimumAmountDenyRule>();
```

La lista negra de SSN vive en la configuración (`appsettings.json`), no en el código.

## Transacción

`LoanStore.SaveApprovedAsync` escribe el cliente, la solicitud **y** el registro del
evento dentro de un único `SaveChangesAsync`, que EF Core envuelve en una sola
transacción de base de datos. O existen los tres, o no existe ninguno:

- Si falla la inserción o actualización del cliente o de la solicitud, no se confirma
  nada y **no se publica ningún evento**, porque el evento es una fila de esa misma
  transacción.
- Si el proceso se cae justo después de confirmar, la fila del evento sobrevive y el
  proceso en segundo plano la entrega en el siguiente ciclo.
- Si el servicio externo está caído, solo falla la entrega: la base de datos queda
  consistente y el mensaje se reintenta.

Por eso el evento se guarda en lugar de enviarse dentro de la petición: una llamada
HTTP no puede formar parte de una transacción de base de datos, así que el enfoque
ingenuo de "guardo y después hago POST" deja los dos sistemas desincronizados cada vez
que el POST falla.

## Cliente recurrente

El caso de uso busca al cliente por su SSN. Si ya existe, actualiza el cliente y su
única solicitud en el mismo registro, en lugar de insertar, y el evento sale marcado
con `isReturningCustomer: true`. Un índice único sobre `Ssn` garantiza el invariante
a nivel de base de datos.

## Evento en segundo plano y servicio externo

`OutboxPublisher` es un `BackgroundService` que cada dos segundos revisa las filas
pendientes de la bandeja de salida, fuera de la petición HTTP que responde al
formulario, y las entrega:

| Caso | Llamada |
| --- | --- |
| Cliente nuevo | `POST /customers` |
| Cliente recurrente | `PUT /customers/{ssn}` |

Contenido del mensaje:

```json
{
  "isReturningCustomer": false,
  "customer": { "firstName": "...", "lastName": "...", "address": "...", "state": "TX", "companyName": "...", "ssn": "555-12-3456" },
  "application": { "id": "...", "requestedAmount": 25000, "customerId": "..." }
}
```

Decisiones de diseño:

- **El SSN es la clave en el servicio externo.** Es la misma clave natural con la que
  el dominio identifica a un cliente recurrente, así que el contrato queda idempotente:
  reenviar un mensaje deja el mismo estado final.
- **Entrega al menos una vez, con tope de 5 intentos.** Los fallos quedan registrados
  en la propia fila (`Attempts`, `LastError`) y se reintentan en el siguiente ciclo.
  Como los endpoints son idempotentes, una entrega duplicada no hace daño.

## Concesiones

- **Bandeja de salida por sondeo en vez de un broker de mensajería.** RabbitMQ o Kafka
  agregarían infraestructura que este alcance no necesita; la bandeja de salida ya da
  la garantía de atomicidad, que es el requisito real.
- **SQLite.** Transacciones reales y ningún servidor que instalar. El proveedor es una
  sola línea en `Program.cs`, así que pasar a SQL Server o PostgreSQL es un cambio de
  configuración.
- **`EnsureCreated()` en lugar de migraciones.** Hay un solo esquema y no hay historial
  de versiones que mantener en una prueba; en un despliegue real, las migraciones
  serían lo primero que agregaría.
- **Sin repositorio por entidad, sin MediatR y sin AutoMapper.** Un único caso de uso no
  justifica esas capas.
- **Cada cliente tiene exactamente una solicitud**, tal como describe el enunciado, y el
  camino del cliente recurrente la actualiza. Permitir varias solicitudes por cliente
  obligaría a decidir cuál se actualiza, y esa regla no está especificada.
- **Los motivos de rechazo se devuelven al navegador.** Sirve para el ejercicio, pero un
  producto real mantendría el motivo real en el servidor (que alguien pueda descubrir
  la lista negra probando el formulario no es deseable) y mostraría un mensaje genérico.
