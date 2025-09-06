import { useEffect, useState } from "react";

interface AlertLog {
    id: number;
    timestamp: string;
    type: string;
    message: string;
    channel: string; // SMS or Email
}

export default function AlertLog() {
    const [alerts, setAlerts] = useState<AlertLog[]>([]);
    const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

    useEffect(() => {
        const fetchAlerts = async () => {
            try {
                const res = await fetch("http://localhost:5213/api/alerts");
                const data = await res.json();
                setAlerts(data);
                setLastUpdated(new Date());
            } catch (err) {
                console.error("Failed to load alerts:", err);
            }
        };

        fetchAlerts();
        const interval = setInterval(fetchAlerts, 10000); // refresh every 10s
        return () => clearInterval(interval);
    }, []);

    if (!alerts.length) return <p>No alerts found.</p>;

    return (
        <div>
            <h2 className="text-xl font-semibold mb-2">Alert History</h2>
            <table className="w-full border-collapse border border-gray-300 text-sm mb-2">
                <thead>
                    <tr className="bg-gray-100">
                        <th className="border p-2">Timestamp</th>
                        <th className="border p-2">Type</th>
                        <th className="border p-2">Channel</th>
                        <th className="border p-2">Message</th>
                    </tr>
                </thead>
                <tbody>
                    {alerts.map((a) => (
                        <tr key={a.id}>
                            <td className="border p-2">
                                {new Date(a.timestamp).toLocaleString()}
                            </td>
                            <td className="border p-2">{a.type}</td>
                            <td className="border p-2">{a.channel}</td>
                            <td className="border p-2">{a.message}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
            {lastUpdated && (
                <p className="text-xs text-gray-500">
                    Last updated: {lastUpdated.toLocaleTimeString()}
                </p>
            )}
        </div>
    );
}
