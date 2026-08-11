import express from "express";

const app = express();
app.use(express.json());

/** In-memory store keyed by SSN: the contract is idempotent per customer. */
const customers = new Map();

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

/** Inspection endpoint, used by the UI and the demo to prove delivery. */
app.get("/customers", (_req, res) => res.json([...customers.values()]));

const port = process.env.PORT || 4000;
app.listen(port, () => console.log(`[external-service] listening on http://localhost:${port}`));
