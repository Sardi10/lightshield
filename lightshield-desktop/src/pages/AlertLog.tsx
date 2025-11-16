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
    const [loading, setLoading] = useState(true);
    const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

    const fetchAlerts = async () => {
        setLoading(true);
        try {
            const res = await fetch("http://localhost:5213/api/alerts");
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();
            setAlerts(Array.isArray(data) ? data : []);
            setLastUpdated(new Date());
        } catch (err) {
            console.error("Failed to load alerts:", err);
            setAlerts([]);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchAlerts();
        const interval = setInterval(fetchAlerts, 10000);
        return () => clearInterval(interval);
    }, []);

    return (
        <div>
            <div className="flex justify-between items-center mb-2">
                <h2 className="text-xl font-semibold">Alert History</h2>
                <button
                    onClick={fetchAlerts}
                    disabled={loading}
                    className="text-sm px-3 py-1 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50"
                >
                    🔄 Refresh Now
                </button>
            </div>

            {loading ? (
                <p>Loading alerts…</p>
            ) : alerts.length === 0 ? (
                <p>No alerts found.</p>
            ) : (
                <>
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
                </>
            )}
        </div>
    );
}
