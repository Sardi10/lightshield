import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";

type Config = {
    id?: number;
    maxFailedLogins: number;
    maxFileDeletes: number;
    maxFileCreates: number;
    maxFileModifies: number;
    phoneNumber: string;
    email: string;
    createdAt?: string;
    updatedAt?: string;
};

const emailOk = (s: string) => (s ? /\S+@\S+\.\S+/.test(s) : true);
const phoneOk = (s: string) => (s ? /^\+\d{8,15}$/.test(s) : true);

export default function ConfigEditor() {
    const [cfg, setCfg] = useState<Config | null>(null);
    const [loading, setLoading] = useState(true);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const [ok, setOk] = useState<string | null>(null);

    useEffect(() => {
        (async () => {
            try {
                const res = await fetch("http://localhost:5213/api/configuration");
                if (!res.ok) throw new Error(`GET /api/configuration -> ${res.status}`);
                const data = await res.json();

                setCfg({
                    ...data,
                    email: data.Email ?? data.email,
                    phoneNumber: data.PhoneNumber ?? data.phoneNumber,
                });

            } catch (e: unknown) {
                setError(e instanceof Error ? e.message : "Failed to load configuration");
            } finally {
                setLoading(false);
            }
        })();
    }, []);

    const setStr = (k: keyof Config) => (e: React.ChangeEvent<HTMLInputElement>) =>
        cfg && setCfg({ ...cfg, [k]: e.target.value });

    const save = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!cfg) return;

        setError(null);
        setOk(null);

        if (!emailOk(cfg.email)) return setError("Invalid email format");
        if (!phoneOk(cfg.phoneNumber)) return setError("Phone must be E.164 (+15551234567)");

        setSaving(true);
        try {
            // ✅ Explicit payload: ONLY fields we want to submit
            const payload = {
                email: cfg.email,
                phoneNumber: cfg.phoneNumber,
            };

            const res = await fetch("http://localhost:5213/api/configuration", {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload),
            });

            if (!res.ok) throw new Error(await res.text());

            const updated: Config = await res.json();
            setCfg(updated);
            setOk("Configuration saved successfully");
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : "Save failed");
        } finally {
            setSaving(false);
        }
    };

    if (loading) return <div className="p-4">Loading…</div>;
    if (!cfg) return <div className="p-4 text-red-600">{error ?? "No configuration found."}</div>;

    return (
        <div
            className="min-h-screen flex flex-col text-gray-900"
            style={{
                background: "linear-gradient(180deg, #3B82F6 0%, #2563EB 60%, #1E40AF 100%)",
            }}
        >
            {/* Header */}
            <header className="bg-gradient-to-r from-blue-600 to-blue-700 text-white py-5 px-8 shadow-md">
                <div className="flex justify-between items-center">
                    <h1 className="text-2xl font-bold tracking-wide drop-shadow">
                        Configuration
                    </h1>
                    <Link
                        to="/dashboard"
                        className="text-white/90 hover:text-white transition font-medium"
                    >
                        ← Back to Dashboard
                    </Link>
                </div>
            </header>

            {/* Main Form */}
            <main className="flex-grow flex justify-center items-start py-10 px-6">
                <form
                    onSubmit={save}
                    className="w-full max-w-3xl bg-white/95 rounded-xl shadow-lg shadow-blue-200/30 p-8 space-y-8"
                >
                    {error && (
                        <div className="p-3 bg-red-100 border border-red-300 rounded text-red-700">
                            {error}
                        </div>
                    )}
                    {ok && (
                        <div className="p-3 bg-green-100 border border-green-300 rounded text-green-700">
                            {ok}
                        </div>
                    )}

                    {/* Alert Thresholds (Read-Only) */}
                    <section>
                        <h2 className="text-lg font-semibold mb-4 text-blue-700">
                            Alert Thresholds (System-Controlled)
                        </h2>
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                            <Num label="Max Failed Logins" value={cfg.maxFailedLogins} disabled />
                            <Num label="Max File Deletes" value={cfg.maxFileDeletes} disabled />
                            <Num label="Max File Creates" value={cfg.maxFileCreates} disabled />
                            <Num label="Max File Modifies" value={cfg.maxFileModifies} disabled />
                        </div>
                        <p className="text-sm text-gray-500 mt-3">
                            These values are managed automatically by LightShield.
                        </p>
                    </section>

                    {/* Contacts */}
                    <section>
                        <h2 className="text-lg font-semibold mb-4 text-blue-700">
                            Alert Contacts
                        </h2>
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                            <Text
                                label="Email"
                                type="email"
                                value={cfg.email}
                                onChange={setStr("email")}
                            />
                            <Text
                                label="Phone (E.164)"
                                type="tel"
                                placeholder="+15551234567"
                                value={cfg.phoneNumber}
                                onChange={setStr("phoneNumber")}
                            />
                        </div>
                    </section>

                    <div className="flex justify-end">
                        <button
                            type="submit"
                            disabled={saving}
                            className="px-6 py-2.5 rounded-lg bg-blue-600 hover:bg-blue-700 text-white font-medium shadow-md transition disabled:opacity-50"
                        >
                            {saving ? "Saving…" : "Save"}
                        </button>
                    </div>
                </form>
            </main>

            <footer className="text-center text-gray-200 text-sm py-4">
                © {new Date().getFullYear()}{" "}
                <span className="text-white font-medium">LightShield</span> — Secure. Lightweight. Reliable.
            </footer>
        </div>
    );
}

/* ========================
   Reusable Inputs
   ======================== */

function Num({
    label,
    value,
    disabled = false,
}: {
    label: string;
    value: number;
    disabled?: boolean;
}) {
    return (
        <label className="flex flex-col">
            <span className="text-sm font-medium text-gray-700">{label}</span>
            <input
                className={`mt-1 p-2 border rounded-md transition
                    ${disabled
                        ? "bg-gray-100 border-gray-200 text-gray-500 cursor-not-allowed"
                        : "border-gray-300 focus:ring-2 focus:ring-blue-400 focus:border-blue-400"
                    }`}
                type="number"
                value={value}
                disabled
            />
        </label>
    );
}

function Text({
    label,
    type,
    value,
    onChange,
    placeholder,
}: {
    label: string;
    type: string;
    value: string;
    onChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
    placeholder?: string;
}) {
    return (
        <label className="flex flex-col">
            <span className="text-sm font-medium text-gray-700">{label}</span>
            <input
                className="mt-1 p-2 border border-gray-300 rounded-md focus:ring-2 focus:ring-blue-400 focus:border-blue-400 transition"
                type={type}
                value={value}
                onChange={onChange}
                placeholder={placeholder}
            />
        </label>
    );
}
