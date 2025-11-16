import { useState } from "react";
import { Link } from "react-router-dom";
import { motion, AnimatePresence } from "framer-motion";
import AnomalyList from "./AnomalyList";
import EventStream from "./EventStream";
import AlertLog from "./AlertLog";

function TabButton({ active, onClick, children }) {
    return (
        <button
            onClick={onClick}
            className={`px-5 py-2 rounded-full text-sm font-semibold transition-all duration-300 ${active
                    ? "bg-orange-500 text-white shadow-md shadow-orange-300/30"
                    : "bg-white text-gray-800 hover:bg-gray-100 shadow-sm"
                }`}
        >
            {children}
        </button>
    );
}

export default function Dashboard() {
    const [tab, setTab] = useState<"anomalies" | "events" | "alerts">("anomalies");

    return (
        <div
            className="min-h-screen text-gray-900 flex flex-col"
            style={{
                background: "linear-gradient(to bottom, #1e3c72, #2a5298)",
            }}
        >
            {/* HEADER */}
            <header className="bg-gradient-to-r from-blue-600 to-blue-700 text-white py-6 px-8 shadow-lg">
                <div className="flex justify-between items-center">
                    <h1 className="text-3xl font-extrabold tracking-wide drop-shadow-sm">
                        LightShield Dashboard
                    </h1>
                    <div className="space-x-6 text-sm font-medium">
                        <Link
                            to="/"
                            className="text-white/90 hover:text-white transition"
                        >
                            Onboarding
                        </Link>
                        <Link
                            to="/config"
                            className="text-white/90 hover:text-white transition"
                        >
                            Configuration
                        </Link>
                    </div>
                </div>
            </header>

            {/* MAIN CONTENT */}
            <main className="flex-grow max-w-6xl mx-auto w-full px-6 py-8">
                {/* Tabs */}
                <div className="flex justify-center mb-8">
                    <div className="flex space-x-3 bg-white/10 backdrop-blur-md p-2 rounded-full shadow-md">
                        <TabButton
                            active={tab === "anomalies"}
                            onClick={() => setTab("anomalies")}
                        >
                            Anomalies
                        </TabButton>
                        <TabButton
                            active={tab === "events"}
                            onClick={() => setTab("events")}
                        >
                            Events
                        </TabButton>
                        <TabButton
                            active={tab === "alerts"}
                            onClick={() => setTab("alerts")}
                        >
                            Alerts
                        </TabButton>
                    </div>
                </div>

                {/* Animated Tab Content */}
                <AnimatePresence mode="wait">
                    {tab === "anomalies" && (
                        <motion.div
                            key="anomalies"
                            initial={{ opacity: 0, y: 10 }}
                            animate={{ opacity: 1, y: 0 }}
                            exit={{ opacity: 0, y: -10 }}
                            transition={{ duration: 0.3 }}
                            className="bg-white/95 rounded-xl shadow-lg shadow-blue-200/30 p-6"
                        >
                            <AnomalyList />
                        </motion.div>
                    )}
                    {tab === "events" && (
                        <motion.div
                            key="events"
                            initial={{ opacity: 0, y: 10 }}
                            animate={{ opacity: 1, y: 0 }}
                            exit={{ opacity: 0, y: -10 }}
                            transition={{ duration: 0.3 }}
                            className="bg-white/95 rounded-xl shadow-lg shadow-blue-200/30 p-6"
                        >
                            <EventStream />
                        </motion.div>
                    )}
                    {tab === "alerts" && (
                        <motion.div
                            key="alerts"
                            initial={{ opacity: 0, y: 10 }}
                            animate={{ opacity: 1, y: 0 }}
                            exit={{ opacity: 0, y: -10 }}
                            transition={{ duration: 0.3 }}
                            className="bg-white/95 rounded-xl shadow-lg shadow-blue-200/30 p-6"
                        >
                            <AlertLog />
                        </motion.div>
                    )}
                </AnimatePresence>
            </main>

            {/* FOOTER */}
            <footer className="text-center text-gray-200 text-sm py-4">
                © {new Date().getFullYear()}{" "}
                <span className="text-white font-medium">LightShield</span> — Secure. Lightweight. Reliable.
            </footer>
        </div>
    );
}
