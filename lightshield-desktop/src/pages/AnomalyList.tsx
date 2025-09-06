import { useEffect, useState } from "react";
import type { Anomaly } from "../types";

export default function AnomalyList() {
    const [anomalies, setAnomalies] = useState<Anomaly[]>([]);
    const [loading, setLoading] = useState(true);
    const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

    useEffect(() => {
        const fetchAnomalies = async () => {
            try {
                const res = await fetch("http://localhost:5213/api/anomalies");
                if (!res.ok) throw new Error(`HTTP ${res.status}`);
                const data = await res.json();

                let list: Anomaly[] = [];
                if (Array.isArray(data)) {
                    list = data as Anomaly[];
                } else if (
                    typeof data === "object" &&
                    data !== null &&
                    "items" in data &&
                    Array.isArray((data as { items?: unknown }).items)
                ) {
                    list = (data as { items: Anomaly[] }).items;
                }

                setAnomalies(list);
                setLastUpdated(new Date());
            } catch (err) {
                console.error("Failed to load anomalies:", err);
                setAnomalies([]);
            } finally {
                setLoading(false);
            }
        };

        fetchAnomalies();
        const interval = setInterval(fetchAnomalies, 5000); // refresh every 5s
        return () => clearInterval(interval);
    }, []);

    return (
        <div>
            <h2 className="text-xl font-semibold mb-2">Anomalies</h2>
            {loading ? (
                <p>Loading anomalies…</p>
            ) : anomalies.length === 0 ? (
                <p>No anomalies found.</p>
            ) : (
                <>
                    <table className="w-full border-collapse border border-gray-300 text-sm mb-2">
                        <thead>
                            <tr className="bg-gray-100">
                                <th className="border p-2">Timestamp</th>
                                <th className="border p-2">Hostname</th>
                                <th className="border p-2">Type</th>
                                <th className="border p-2">Description</th>
                            </tr>
                        </thead>
                        <tbody>
                            {anomalies.map((a) => (
                                <tr key={a.id}>
                                    <td className="border p-2">
                                        {new Date(a.timestamp).toLocaleString()}
                                    </td>
                                    <td className="border p-2">{a.hostname}</td>
                                    <td className="border p-2">{a.type}</td>
                                    <td className="border p-2">{a.description}</td>
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
