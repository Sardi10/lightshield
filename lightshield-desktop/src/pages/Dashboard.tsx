// src/pages/Dashboard.tsx
import { useEffect, useState } from "react";
import type { Anomaly } from "../types";
import { Link } from "react-router-dom";
import EventStream from "./EventStream";
import AlertLog from "./AlertLog";

interface TabButtonProps {
    active: boolean;
    onClick: () => void;
    children: React.ReactNode;
}

function TabButton({ active, onClick, children }: TabButtonProps) {
    return (
        <button
            onClick={onClick}
            className={`px-4 py-2 border-b-2 ${active
                    ? "border-blue-600 text-blue-600"
                    : "border-transparent text-gray-600 hover:text-blue-600"
                }`}
        >
            {children}
        </button>
    );
}

export default function Dashboard() {
    const [anomalies, setAnomalies] = useState<Anomaly[]>([]);
    const [loading, setLoading] = useState(true);
    const [tab, setTab] = useState<"anomalies" | "events" | "alerts">(
        "anomalies"
    );

    useEffect(() => {
        if (tab !== "anomalies") return;

        fetch("http://localhost:5213/api/anomalies")
            .then((res) => {
                if (!res.ok) throw new Error(`HTTP ${res.status}`);
                return res.json() as unknown;
            })
            .then((json: unknown) => {
                let list: Anomaly[] = [];
                if (Array.isArray(json)) {
                    list = json as Anomaly[];
                } else if (
                    typeof json === "object" &&
                    json !== null &&
                    "items" in json &&
                    Array.isArray((json as { items?: unknown }).items)
                ) {
                    list = (json as { items: Anomaly[] }).items;
                }
                setAnomalies(list);
            })
            .catch((err) => {
                console.error(err);
                setAnomalies([]);
            })
            .finally(() => setLoading(false));
    }, [tab]);

    return (
        <div className="p-8 max-w-4xl mx-auto">
            <h1 className="text-2xl font-semibold mb-4">Dashboard</h1>

            <div className="mb-4 space-x-4">
                <Link to="/" className="text-blue-600 hover:underline">
                    ← Back to Onboarding
                </Link>
                <Link to="/config" className="text-blue-600 hover:underline">
                    ⚙️ Edit Configuration
                </Link>
            </div>

            {/* Tabs */}
            <div className="flex space-x-4 border-b mb-4">
                <TabButton active={tab === "anomalies"} onClick={() => setTab("anomalies")}>
                    Anomalies
                </TabButton>
                <TabButton active={tab === "events"} onClick={() => setTab("events")}>
                    Events
                </TabButton>
                <TabButton active={tab === "alerts"} onClick={() => setTab("alerts")}>
                    Alerts
                </TabButton>
            </div>

            {/* Tab Content */}
            <div>
                {tab === "anomalies" && (
                    <div>
                        {loading ? (
                            <p>Loading…</p>
                        ) : anomalies.length === 0 ? (
                            <p>No anomalies found.</p>
                        ) : (
                            <ul className="list-disc pl-5 space-y-2">
                                {anomalies.map((a) => (
                                    <li key={a.id}>
                                        <strong>{a.type}</strong> on <em>{a.hostname}</em> at{" "}
                                        {new Date(a.timestamp).toLocaleString()}
                                    </li>
                                ))}
                            </ul>
                        )}
                    </div>
                )}
                {tab === "events" && <EventStream />}
                {tab === "alerts" && <AlertLog />}
            </div>
        </div>
    );
}
