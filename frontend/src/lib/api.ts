export const US_STATES = [
  "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA",
  "HI", "ID", "IL", "IN", "IA", "KS", "KY", "LA", "ME", "MD",
  "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ",
  "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC",
  "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV", "WI", "WY",
] as const;

export type ApplicationForm = {
  firstName: string;
  lastName: string;
  address: string;
  state: string;
  companyName: string;
  requestedAmount: string;
  ssn: string;
};

export type SubmitResponse = {
  status: "approved" | "denied";
  reason: string | null;
  returningCustomer: boolean;
};

const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5137";

export async function submitApplication(form: ApplicationForm): Promise<SubmitResponse> {
  const response = await fetch(`${API_URL}/api/applications`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ ...form, requestedAmount: Number(form.requestedAmount) }),
  });

  if (!response.ok) {
    throw new Error("We could not process your application right now. Please try again.");
  }

  return response.json();
}
