# Fundo — Flujo de solicitud de préstamo

> **Video de la demo:** _PENDIENTE: pegar aquí el enlace público (Loom) antes de enviar el repositorio._

_English version: [README.md](README.md)_

Flujo completo de solicitud de préstamo: un formulario en Next.js, un backend en .NET
donde un motor de reglas decide la aprobación, guardado transaccional en base de datos
y un evento en segundo plano que envía el resultado a un servicio externo por HTTP.

```
frontend (Next.js :3000) → API (.NET :5137) → SQLite (loan.db)
                                    │
                                    └── bandeja de salida → proceso en segundo plano → servicio externo simulado (:4000)
```

## Requisitos

- SDK de .NET 10
- Node.js 20 o superior

No hace falta instalar ningún servidor de base de datos: se usa un archivo SQLite que
se crea solo en el primer arranque.

## Cómo levantar todo en local

Tres terminales, desde la raíz del repositorio.

```bash
# 1. Servicio externo simulado — http://localhost:4000
cd mock-service
npm install
npm start

# 2. API del backend — http://localhost:5137
dotnet run --project backend/Loan.Api --launch-profile http

# 3. Frontend — http://localhost:3000
cd frontend
npm install
npm run dev
```

Abrir http://localhost:3000.

Para empezar de cero, detener la API y borrar `backend/Loan.Api/loan.db`.

## Cómo correr las pruebas

```bash
dotnet test
```

Son 13 pruebas: motor de reglas, camino del cliente recurrente, reversión de la
transacción y el endpoint HTTP.

## Datos de prueba

| Caso | Qué escribir |
| --- | --- |
| **Aprobado** | Cualquier estado distinto de NY y un SSN que no esté en la lista negra. Por ejemplo: estado `TX`, SSN `555-12-3456`. |
| **Rechazo por estado** | Estado `NY`, con cualquier SSN. |
| **Rechazo por SSN** | SSN `111-11-1111`, `222-22-2222` o `333-33-3333` (configurados en `backend/Loan.Api/appsettings.json`, sección `BlacklistedSsns`). |
| **Cliente recurrente** | Enviar una solicitud aprobada y volver a enviarla con **el mismo SSN** cambiando el monto o la empresa. La pantalla indica que la solicitud se actualizó, y en la base de datos sigue habiendo un solo cliente y una sola solicitud. |

Para ver en cualquier momento qué recibió el servicio externo:

```bash
curl http://localhost:4000/customers
```

El servicio simulado también escribe cada llamada en su consola:
`[external-service] CREATE 555-12-3456 amount=25000`.

## Decisiones y lo que quedó fuera a propósito

- **Sin autenticación**, tal como indica el enunciado.
- **SQLite** en lugar de SQL Server o PostgreSQL: transacciones reales sin que quien
  revise tenga que instalar nada.
- El proceso en segundo plano consulta la bandeja de salida por sondeo, con un tope de
  reintentos; no se usa ningún broker de mensajería.
- **Sin Docker, sin integración continua y sin datos semilla.** El razonamiento de cada
  uno está en [ARCHITECTURE.es.md](ARCHITECTURE.es.md), junto con el resto de decisiones.

La interfaz está en inglés porque el producto y sus términos (SSN, state) son de
Estados Unidos; la documentación y los comentarios del código están en español.
