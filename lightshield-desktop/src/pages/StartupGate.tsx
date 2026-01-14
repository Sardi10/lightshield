import { useEffect } from "react";
import { useNavigate } from "react-router-dom";

export default function StartupGate() {
    const navigate = useNavigate();

    useEffect(() => {
        const checkConfig = async () => {
            try {
                const res = await fetch("http://localhost:5213/api/configuration");

                if (!res.ok) {
                    navigate("/onboarding");
                    return;
                }

                const data = await res.json();

                // Backend DTO uses PascalCase
                const email = data?.Email;
                const phone = data?.PhoneNumber;

                if (email && phone) {
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
