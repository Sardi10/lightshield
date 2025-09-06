import { useState } from "react";
import { Link } from "react-router-dom";
import AnomalyList from "./AnomalyList";
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
    const [tab, setTab] = useState<"anomalies" | "events" | "alerts">("anomalies");

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
            <div className="mt-4">
                {tab === "anomalies" && <AnomalyList />}
                {tab === "events" && <EventStream />}
                {tab === "alerts" && <AlertLog />}
            </div>
        </div>
    );
}
