import { useState } from "react";
import { useNavigate } from "react-router-dom";

export default function Onboarding() {
    const [phone, setPhone] = useState("");
    const [email, setEmail] = useState("");
    const [telegramBotToken, setTelegramBotToken] = useState("");
    const [telegramChatId, setTelegramChatId] = useState("");

    const [error, setError] = useState<string | null>(null);
    const [loading, setLoading] = useState(false);
    const navigate = useNavigate();

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError(null);

        const hasEmail = email.trim() !== "";
        const hasPhone = phone.trim() !== "";
        const hasTelegram =
            telegramBotToken.trim() !== "" &&
            telegramChatId.trim() !== "";

        if (!hasEmail && !hasPhone && !hasTelegram) {
            setError(
                "Please provide at least one alert method: Email, SMS, or Telegram."
            );
            return;
        }

        setLoading(true);

        try {
            const res = await fetch("http://localhost:5213/api/configuration", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    phoneNumber: phone,
                    email,
                    telegramBotToken,
                    telegramChatId
                }),
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
            className="min-h-screen flex justify-center text-gray-800 py-6 px-4"
            style={{
                background:
                    "linear-gradient(180deg, #3B82F6 0%, #2563EB 60%, #1E40AF 100%)",
            }}
        >
            <div className="bg-white/95 rounded-2xl shadow-xl shadow-blue-200/40 p-6 w-full max-w-md text-center space-y-4 self-center">
                {/* Header */}
                <div className="flex flex-col items-center space-y-1">
                    <div className="w-12 h-12 rounded-full bg-blue-100 flex items-center justify-center shadow-md">
                        <span className="text-2xl text-blue-600">🛡️</span>
                    </div>
                    <h1 className="text-xl font-bold text-gray-900">
                        Welcome to LightShield
                    </h1>
                    <p className="text-xs text-gray-500">
                        Choose how you want to receive security alerts.
                    </p>
                </div>

                {/* Form */}
                <form onSubmit={handleSubmit} className="space-y-3 text-left">
                    {/* EMAIL */}
                    <div>
                        <label className="block text-sm font-medium mb-1">
                            Email (Required)
                        </label>
                        <input
                            type="email"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                            className="w-full border border-gray-300 rounded-md p-2 focus:ring-2 focus:ring-blue-400 focus:border-blue-400 transition"
                            placeholder="alerts@company.com"
                        />
                    </div>

                    {/* SMS */}
                    <div>
                        <label className="block text-sm font-medium mb-0.5">
                            Phone number (optional)
                        </label>
                        <p className="text-[11px] text-gray-500 mb-1">
                            Used for SMS alerts only if an SMS provider is configured.
                        </p>
                        <input
                            type="text"
                            value={phone}
                            onChange={(e) => setPhone(e.target.value)}
                            className="w-full border border-gray-300 rounded-md p-2 focus:ring-2 focus:ring-blue-400 focus:border-blue-400 transition"
                            placeholder="+15551234567"
                        />
                    </div>

                    {/* TELEGRAM */}
                    <div className="mt-3 p-3 rounded-lg bg-blue-50 border border-blue-200">
                        <h3 className="font-semibold text-sm text-blue-800 mb-1">
                            Telegram Alerts (Recommended)
                        </h3>
                        <p className="text-[11px] text-blue-700 mb-2">
                            Instant, reliable alerts without carrier delays or SMS filtering.
                        </p>

                        <input
                            type="text"
                            value={telegramBotToken}
                            onChange={(e) => setTelegramBotToken(e.target.value)}
                            className="w-full border border-gray-300 rounded-md p-2 mb-1"
                            placeholder="Telegram Bot Token"
                        />

                        <input
                            type="text"
                            value={telegramChatId}
                            onChange={(e) => setTelegramChatId(e.target.value)}
                            className="w-full border border-gray-300 rounded-md p-2"
                            placeholder="Telegram Chat ID"
                        />

                        <p className="text-[11px] text-blue-700 mt-1">
                            You must open the bot in Telegram and press <b>Start</b> before messages can be delivered.
                        </p>
                    </div>

                    {error && (
                        <p className="text-red-600 text-xs bg-red-50 border border-red-300 rounded-md px-3 py-2">
                            {error}
                        </p>
                    )}

                    <button
                        type="submit"
                        disabled={loading}
                        className="w-full bg-blue-600 hover:bg-blue-700 text-white py-2 rounded-lg text-sm font-medium shadow-md transition disabled:opacity-50"
                    >
                        {loading ? "Saving..." : "Save & Continue"}
                    </button>
                </form>

                {/* Footer */}
                <p className="text-[10px] text-gray-400 pt-1">
                    © {new Date().getFullYear()} LightShield — Secure. Lightweight. Reliable.
                </p>
            </div>
        </div>
    );
}
