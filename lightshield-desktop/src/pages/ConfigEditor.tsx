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
                const data: Config = await res.json();
                setCfg(data);
            } catch (e: unknown) {
                setError(e instanceof Error ? e.message : "Failed to load configuration");
            } finally {
                setLoading(false);
            }
        })();
    }, []);

    const setNum = (k: keyof Config) => (e: React.ChangeEvent<HTMLInputElement>) =>
        cfg && setCfg({ ...cfg, [k]: Math.max(0, Number(e.target.value || 0)) });

    const setStr = (k: keyof Config) => (e: React.ChangeEvent<HTMLInputElement>) =>
        cfg && setCfg({ ...cfg, [k]: e.target.value });

    const save = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!cfg) return;
        setError(null); setOk(null);

        if (!emailOk(cfg.email)) return setError("Invalid email format");
        if (!phoneOk(cfg.phoneNumber)) return setError("Phone must be E.164 (+15551234567)");

        setSaving(true);
        try {
            const res = await fetch("http://localhost:5213/api/configuration", {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(cfg),
            });
            if (!res.ok) throw new Error(await res.text());
            const updated: Config = await res.json();
            setCfg(updated);
            setOk("Configuration saved");
        } catch (e: unknown) {
            setError(e instanceof Error ? e.message : "Save failed");
        } finally {
            setSaving(false);
        }
    };

    if (loading) return <div className="p-4">Loading…</div>;
    if (!cfg) return <div className="p-4 text-red-600">{error ?? "No configuration found."}</div>;

    return (
        <form onSubmit={save} className="p-6 max-w-3xl space-y-6">
            <h1 className="text-2xl font-semibold">Configuration</h1>

            {/* ✅ Back to Dashboard button at top */}
            <div className="mb-4">
                <Link to="/dashboard" className="text-blue-600 hover:underline">
                    ← Back to Dashboard
                </Link>
            </div>

            {error && <div className="p-3 bg-red-100 border border-red-300 rounded">{error}</div>}
            {ok && <div className="p-3 bg-green-100 border border-green-300 rounded">{ok}</div>}

            <section>
                <h2 className="font-medium mb-2">Alert Thresholds</h2>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <Num label="Max Failed Logins" value={cfg.maxFailedLogins} onChange={setNum("maxFailedLogins")} />
                    <Num label="Max File Deletes" value={cfg.maxFileDeletes} onChange={setNum("maxFileDeletes")} />
                    <Num label="Max File Creates" value={cfg.maxFileCreates} onChange={setNum("maxFileCreates")} />
                    <Num label="Max File Modifies" value={cfg.maxFileModifies} onChange={setNum("maxFileModifies")} />
                </div>
            </section>

            <section>
                <h2 className="font-medium mb-2">Contacts</h2>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <Text label="Email" type="email" value={cfg.email} onChange={setStr("email")} />
                    <Text label="Phone (E.164)" type="tel" placeholder="+15551234567" value={cfg.phoneNumber} onChange={setStr("phoneNumber")} />
                </div>
            </section>

            <button
                type="submit"
                disabled={saving}
                className="px-4 py-2 rounded bg-blue-600 text-white disabled:opacity-50"
            >
                {saving ? "Saving…" : "Save"}
            </button>
        </form>
    );
}

function Num(props: { label: string; value: number; onChange: (e: React.ChangeEvent<HTMLInputElement>) => void }) {
    return (
        <label className="flex flex-col">
            <span>{props.label}</span>
            <input className="mt-1 p-2 border rounded" type="number" min={0} value={props.value} onChange={props.onChange} />
        </label>
    );
}

function Text(props: { label: string; type: string; value: string; onChange: (e: React.ChangeEvent<HTMLInputElement>) => void; placeholder?: string }) {
    return (
        <label className="flex flex-col">
            <span>{props.label}</span>
            <input className="mt-1 p-2 border rounded" type={props.type} value={props.value} onChange={props.onChange} placeholder={props.placeholder} />
        </label>
    );
}
