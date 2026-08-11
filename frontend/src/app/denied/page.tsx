import Link from "next/link";

export default async function DeniedPage({
  searchParams,
}: {
  searchParams: Promise<{ reason?: string }>;
}) {
  const { reason } = await searchParams;

  return (
    <div className="rounded-xl border border-red-200 bg-white p-8 shadow-sm">
      <p className="text-sm font-semibold uppercase tracking-wide text-red-600">Not approved</p>
      <h1 className="mt-2 text-3xl font-semibold tracking-tight">We cannot move forward</h1>
      <p className="mt-3 text-slate-600">
        {reason || "Your application does not meet our current eligibility criteria."}
      </p>
      <Link
        href="/"
        className="mt-6 inline-block rounded-lg border border-slate-300 px-4 py-2 font-medium transition hover:bg-slate-50"
      >
        Back to the form
      </Link>
    </div>
  );
}
