import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { apiFetch } from "../api";

export default function StartupGate() {
    const navigate = useNavigate();

    useEffect(() => {
        const checkConfig = async () => {
            try {
                const res = await apiFetch("/api/configuration");

                if (!res.ok) {
                    navigate("/onboarding");
                    return;
                }

                const data = await res.json();

                const email = data?.email;
                const phone = data?.phoneNumber;
                const telegram = data?.telegramBotToken && data?.telegramChatId;

                if (email || phone || telegram) {
                    navigate("/dashboard");
                } else {
                    navigate("/onboarding");
                }
            } catch {
                // Backend unreachable or first run
                navigate("/onboarding");
            }

        };

        checkConfig();
    }, [navigate]);

    return (
        <div className="min-h-screen flex items-center justify-center text-gray-500">
            Loading LightShield…
        </div>
    );
}
