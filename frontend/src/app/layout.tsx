import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Fundo — Loan Application",
  description: "Apply for working capital in minutes.",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className="min-h-screen bg-slate-50 text-slate-900 antialiased">
        <header className="border-b border-slate-200 bg-white">
          <div className="mx-auto flex max-w-3xl items-center gap-3 px-6 py-4">
            <span className="grid h-8 w-8 place-items-center rounded-lg bg-emerald-600 text-sm font-bold text-white">
              F
            </span>
            <span className="text-lg font-semibold tracking-tight">Fundo</span>
          </div>
        </header>
        <main className="mx-auto max-w-3xl px-6 py-10">{children}</main>
      </body>
    </html>
  );
}
