import express from "express";

const app = express();
app.use(express.json());

/** Almacén en memoria indexado por SSN: el contrato es idempotente por cliente. */
const customers = new Map();

/** Índice del servicio: qué es esto y qué endpoints expone. */
app.get("/", (_req, res) =>
  res.json({
    service: "Mock external service",
    description: "Stands in for the third-party system that receives approved applications.",
    endpoints: {
      "POST /customers": "Create a customer (new customer event)",
      "PUT /customers/:ssn": "Update a customer (returning customer event)",
      "GET /customers": "Everything received so far, for inspection",
    },
    received: customers.size,
  })
);

app.post("/customers", (req, res) => {
  const ssn = req.body?.customer?.ssn;
  if (!ssn) return res.status(400).json({ error: "customer.ssn is required" });

  customers.set(ssn, { ...req.body, receivedAt: new Date().toISOString(), operation: "created" });
  console.log(`[external-service] CREATE ${ssn} amount=${req.body?.application?.requestedAmount}`);
  res.status(200).json({ status: "ok", operation: "created", ssn });
});

app.put("/customers/:ssn", (req, res) => {
  const { ssn } = req.params;
  customers.set(ssn, { ...req.body, receivedAt: new Date().toISOString(), operation: "updated" });
  console.log(`[external-service] UPDATE ${ssn} amount=${req.body?.application?.requestedAmount}`);
  res.status(200).json({ status: "ok", operation: "updated", ssn });
});

/** Endpoint de inspección: lo usa la demo para comprobar qué llegó. */
app.get("/customers", (_req, res) => res.json([...customers.values()]));

const port = process.env.PORT || 4000;
app.listen(port, () => console.log(`[external-service] listening on http://localhost:${port}`));
