import Link from "next/link";

export default async function ApprovedPage({
  searchParams,
}: {
  searchParams: Promise<{ returning?: string }>;
}) {
  const { returning } = await searchParams;
  const isReturning = returning === "true";

  return (
    <div className="rounded-xl border border-emerald-200 bg-white p-8 shadow-sm">
      <p className="text-sm font-semibold uppercase tracking-wide text-emerald-600">Approved</p>
      <h1 className="mt-2 text-3xl font-semibold tracking-tight">
        {isReturning ? "Your application was updated" : "Your application was approved"}
      </h1>
      <p className="mt-3 text-slate-600">
        {isReturning
          ? "We already had you on file, so we updated your existing record with the details you just sent."
          : "We saved your information and a specialist will contact you with the next steps."}
      </p>
      <Link
        href="/"
        className="mt-6 inline-block rounded-lg border border-slate-300 px-4 py-2 font-medium transition hover:bg-slate-50"
      >
        Submit another application
      </Link>
    </div>
  );
}
