"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { ApplicationForm, submitApplication, US_STATES } from "@/lib/api";

const EMPTY_FORM: ApplicationForm = {
  firstName: "",
  lastName: "",
  address: "",
  state: "",
  companyName: "",
  requestedAmount: "",
  ssn: "",
};

const SSN_PATTERN = /^\d{3}-?\d{2}-?\d{4}$/;

function validate(form: ApplicationForm): Partial<Record<keyof ApplicationForm, string>> {
  const errors: Partial<Record<keyof ApplicationForm, string>> = {};
  if (!form.firstName.trim()) errors.firstName = "First name is required.";
  if (!form.lastName.trim()) errors.lastName = "Last name is required.";
  if (!form.address.trim()) errors.address = "Address is required.";
  if (!form.state) errors.state = "State is required.";
  if (!form.companyName.trim()) errors.companyName = "Company name is required.";
  if (!form.requestedAmount || Number(form.requestedAmount) <= 0)
    errors.requestedAmount = "Enter an amount greater than zero.";
  if (!SSN_PATTERN.test(form.ssn.trim())) errors.ssn = "Use the format 123-45-6789.";
  return errors;
}

export default function ApplicationPage() {
  const router = useRouter();
  const [form, setForm] = useState<ApplicationForm>(EMPTY_FORM);
  const [errors, setErrors] = useState<Partial<Record<keyof ApplicationForm, string>>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const update = (field: keyof ApplicationForm) => (
    event: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => setForm((current) => ({ ...current, [field]: event.target.value }));

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setSubmitError(null);

    const validationErrors = validate(form);
    setErrors(validationErrors);
    if (Object.keys(validationErrors).length > 0) return;

    setSubmitting(true);
    try {
      const result = await submitApplication(form);
      if (result.status === "approved") {
        router.push(`/approved?returning=${result.returningCustomer}`);
      } else {
        router.push(`/denied?reason=${encodeURIComponent(result.reason ?? "")}`);
      }
    } catch (error) {
      setSubmitError(error instanceof Error ? error.message : "Unexpected error.");
      setSubmitting(false);
    }
  }

  return (
    <div>
      <h1 className="text-3xl font-semibold tracking-tight">Apply for funding</h1>
      <p className="mt-2 text-slate-600">
        Tell us about you and your business. It takes about a minute and we answer right away.
      </p>

      <form onSubmit={handleSubmit} noValidate className="mt-8 space-y-6">
        <section className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">About you</h2>
          <div className="mt-4 grid gap-4 sm:grid-cols-2">
            <Field label="First name" error={errors.firstName}>
              <input
                id="firstName"
                name="firstName"
                value={form.firstName}
                onChange={update("firstName")}
                className={inputClass(errors.firstName)}
                autoComplete="given-name"
              />
            </Field>
            <Field label="Last name" error={errors.lastName}>
              <input
                id="lastName"
                name="lastName"
                value={form.lastName}
                onChange={update("lastName")}
                className={inputClass(errors.lastName)}
                autoComplete="family-name"
              />
            </Field>
            <Field label="Address" error={errors.address} className="sm:col-span-2">
              <input
                id="address"
                name="address"
                value={form.address}
                onChange={update("address")}
                placeholder="123 Main St, Austin"
                className={inputClass(errors.address)}
                autoComplete="street-address"
              />
            </Field>
            <Field label="State" error={errors.state}>
              <select
                id="state"
                name="state"
                value={form.state}
                onChange={update("state")}
                className={inputClass(errors.state)}
              >
                <option value="">Select a state</option>
                {US_STATES.map((state) => (
                  <option key={state} value={state}>
                    {state}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="SSN" error={errors.ssn} hint="Format 123-45-6789">
              <input
                id="ssn"
                name="ssn"
                value={form.ssn}
                onChange={update("ssn")}
                placeholder="123-45-6789"
                inputMode="numeric"
                className={inputClass(errors.ssn)}
              />
            </Field>
          </div>
        </section>

        <section className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-500">Your business</h2>
          <div className="mt-4 grid gap-4 sm:grid-cols-2">
            <Field label="Company name" error={errors.companyName}>
              <input
                id="companyName"
                name="companyName"
                value={form.companyName}
                onChange={update("companyName")}
                className={inputClass(errors.companyName)}
                autoComplete="organization"
              />
            </Field>
            <Field label="Requested amount (USD)" error={errors.requestedAmount}>
              <input
                id="requestedAmount"
                name="requestedAmount"
                value={form.requestedAmount}
                onChange={update("requestedAmount")}
                type="number"
                min={1}
                step={100}
                placeholder="25000"
                className={inputClass(errors.requestedAmount)}
              />
            </Field>
          </div>
        </section>

        {submitError && (
          <p role="alert" className="rounded-lg bg-red-50 px-4 py-3 text-sm text-red-700">
            {submitError}
          </p>
        )}

        <button
          type="submit"
          disabled={submitting}
          className="w-full rounded-lg bg-emerald-600 px-5 py-3 font-medium text-white transition hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
        >
          {submitting ? "Checking your application…" : "Submit application"}
        </button>
      </form>
    </div>
  );
}

function Field({
  label,
  error,
  hint,
  className = "",
  children,
}: {
  label: string;
  error?: string;
  hint?: string;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <label className={`block ${className}`}>
      <span className="mb-1.5 block text-sm font-medium text-slate-700">{label}</span>
      {children}
      {error ? (
        <span className="mt-1 block text-sm text-red-600">{error}</span>
      ) : hint ? (
        <span className="mt-1 block text-sm text-slate-500">{hint}</span>
      ) : null}
    </label>
  );
}

function inputClass(error?: string) {
  return `w-full rounded-lg border px-3 py-2 outline-none transition focus:ring-2 ${
    error
      ? "border-red-400 focus:ring-red-200"
      : "border-slate-300 focus:border-emerald-500 focus:ring-emerald-200"
  }`;
}
