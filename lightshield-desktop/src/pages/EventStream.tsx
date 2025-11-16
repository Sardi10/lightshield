import { useEffect, useState } from "react";

interface Event {
    id: number;
    timestamp: string;
    source: string;
    type: string;
    pathOrMessage: string;
    hostname: string;
}

export default function EventStream() {
    const [events, setEvents] = useState<Event[]>([]);
    const [loading, setLoading] = useState(true);
    const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

    const fetchEvents = async () => {
        setLoading(true);
        try {
            const res = await fetch("http://localhost:5213/api/events");
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            const data = await res.json();
            setEvents(Array.isArray(data) ? data : []);
            setLastUpdated(new Date());
        } catch (err) {
            console.error("Failed to load events:", err);
            setEvents([]);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchEvents();
        const interval = setInterval(fetchEvents, 5000);
        return () => clearInterval(interval);
    }, []);

    return (
        <div>
            <div className="flex justify-between items-center mb-2">
                <h2 className="text-xl font-semibold">Live Event Stream</h2>
                <button
                    onClick={fetchEvents}
                    disabled={loading}
                    className="text-sm px-3 py-1 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50"
                >
                    🔄 Refresh Now
                </button>
            </div>

            {loading ? (
                <p>Loading events…</p>
            ) : events.length === 0 ? (
                <p>No events found.</p>
            ) : (
                <>
                    <table className="w-full border-collapse border border-gray-300 text-sm mb-2">
                        <thead>
                            <tr className="bg-gray-100">
                                <th className="border p-2">Timestamp</th>
                                <th className="border p-2">Hostname</th>
                                <th className="border p-2">Type</th>
                                <th className="border p-2">Source</th>
                                <th className="border p-2">Message</th>
                            </tr>
                        </thead>
                        <tbody>
                            {events.map((e) => (
                                <tr key={e.id}>
                                    <td className="border p-2">
                                        {new Date(e.timestamp).toLocaleString()}
                                    </td>
                                    <td className="border p-2">{e.hostname}</td>
                                    <td className="border p-2">{e.type}</td>
                                    <td className="border p-2">{e.source}</td>
                                    <td className="border p-2">{e.pathOrMessage}</td>
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
