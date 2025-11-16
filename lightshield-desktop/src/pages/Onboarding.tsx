import { useState } from "react";
import { useNavigate } from "react-router-dom";

export default function Onboarding() {
    const [phone, setPhone] = useState("");
    const [email, setEmail] = useState("");
    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState(false);
    const navigate = useNavigate();

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError(null);
        setLoading(true);
        try {
            const res = await fetch("http://localhost:5213/api/configuration", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ phoneNumber: phone, email }),
            });
            if (!res.ok) {
                const body = await res.json();
                throw new Error(body.error || "Failed to save configuration");
            }
            navigate("/dashboard");
        } catch (err: unknown) {
            if (err instanceof Error) setError(err.message);
            else setError(String(err));
        } finally {
            setLoading(false);
        }
    };

    return (
        <div
            className="min-h-screen flex items-center justify-center text-gray-800"
            style={{
                background: "linear-gradient(180deg, #3B82F6 0%, #2563EB 60%, #1E40AF 100%)",
            }}
        >
            <div className="bg-white/95 rounded-2xl shadow-xl shadow-blue-200/40 p-10 w-full max-w-md text-center space-y-6">
                {/* Logo / Header */}
                <div className="flex flex-col items-center space-y-2">
                    <div className="w-16 h-16 rounded-full bg-blue-100 flex items-center justify-center shadow-md">
                        <span className="text-3xl text-blue-600">🛡️</span>
                    </div>
                    <h1 className="text-2xl font-bold text-gray-900">
                        Welcome to LightShield
                    </h1>
                    <p className="text-gray-500 text-sm">
                        Protect your system with smart monitoring.
                    </p>
                </div>

                {/* Form */}
                <form onSubmit={handleSubmit} className="space-y-5 text-left">
                    <div>
                        <label className="block text-sm font-medium mb-1">
                            Phone (E.164)
                        </label>
                        <input
                            type="text"
                            value={phone}
                            onChange={(e) => setPhone(e.target.value)}
                            className="w-full border border-gray-300 rounded-md p-2.5 focus:ring-2 focus:ring-blue-400 focus:border-blue-400 transition"
                            placeholder="+15551234567"
                            required
                        />
                    </div>
                    <div>
                        <label className="block text-sm font-medium mb-1">Email</label>
                        <input
                            type="email"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                            className="w-full border border-gray-300 rounded-md p-2.5 focus:ring-2 focus:ring-blue-400 focus:border-blue-400 transition"
                            placeholder="alerts@company.com"
                            required
                        />
                    </div>

                    {error && (
                        <p className="text-red-600 text-sm bg-red-50 border border-red-300 rounded-md px-3 py-2">
                            {error}
                        </p>
                    )}

                    <button
                        type="submit"
                        disabled={loading}
                        className="w-full bg-blue-600 hover:bg-blue-700 text-white py-2.5 rounded-lg font-medium shadow-md transition disabled:opacity-50"
                    >
                        {loading ? "Saving..." : "Save & Continue"}
                    </button>
                </form>

                {/* Footer */}
                <p className="text-xs text-gray-400">
                    © {new Date().getFullYear()} LightShield — Secure. Lightweight. Reliable.
                </p>
            </div>
        </div>
    );
}
