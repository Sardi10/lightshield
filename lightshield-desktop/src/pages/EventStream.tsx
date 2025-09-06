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
    const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

    useEffect(() => {
        const fetchEvents = async () => {
            try {
                const res = await fetch("http://localhost:5213/api/events");
                const data = await res.json();
                setEvents(data);
                setLastUpdated(new Date());
            } catch (err) {
                console.error("Failed to load events:", err);
            }
        };

        fetchEvents();
        const interval = setInterval(fetchEvents, 5000); // refresh every 5s
        return () => clearInterval(interval);
    }, []);

    if (!events.length) return <p>No events found.</p>;

    return (
        <div>
            <h2 className="text-xl font-semibold mb-2">Live Event Stream</h2>
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
        </div>
    );
}
