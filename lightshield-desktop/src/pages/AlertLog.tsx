import { useEffect, useState } from "react";

interface AlertLogEntry {
    id: number;
    timestamp: string;
    type: string;
    message: string;
    channel: string;

    // Optional IDS metadata
    severity?: string;
    operatingSystem?: string;
    username?: string;
    ipAddress?: string;
    country?: string;
    city?: string;
}

export default function AlertLog() {
    const [alerts, setAlerts] = useState<AlertLogEntry[]>([]);

    // Filters
    const [search, setSearch] = useState("");
    const [startDate, setStartDate] = useState("");
    const [endDate, setEndDate] = useState("");
    const [sortBy, setSortBy] = useState("timestamp");

    // Pagination
    const [page, setPage] = useState(1);
    const pageSize = 10;
    const [totalCount, setTotalCount] = useState(0);
    const totalPages = Math.ceil(totalCount / pageSize);

    // UI State
    const [loading, setLoading] = useState(true);
    const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

    // --------------------------------------------------------------------
    // Fetch Alerts
    // --------------------------------------------------------------------
    const fetchAlerts = async () => {
        setLoading(true);

        const params = new URLSearchParams({
            page: page.toString(),
            pageSize: pageSize.toString(),
            sortBy,
            sortDir: "desc"
        });

        if (search) params.append("search", search);
        if (startDate) params.append("startDate", startDate);
        if (endDate) params.append("endDate", endDate);

        try {
            const res = await fetch(
                `http://localhost:5213/api/alerts?${params.toString()}`
            );
            const data = await res.json();

            const newTotalPages = Math.ceil(data.totalCount / pageSize);

            // Prevent reset if new data has fewer pages
            if (page > newTotalPages && newTotalPages > 0) {
                setPage(newTotalPages);
                setLoading(false);
                setLastUpdated(new Date());
                return;
            }

            setAlerts(data.items);
            setTotalCount(data.totalCount);
            setLastUpdated(new Date());

        } catch (err) {
            console.error("Failed to load alerts:", err);
            setAlerts([]);
        } finally {
            setLoading(false);
        }
    };

    // Fetch on filter changes
    useEffect(() => {
        fetchAlerts();
    }, [page, search, startDate, endDate, sortBy]);

    // Auto-refresh (10s) — pagination safe
    useEffect(() => {
        const interval = setInterval(fetchAlerts, 10000);
        return () => clearInterval(interval);
    }, [page, search, startDate, endDate, sortBy]);

    // --------------------------------------------------------------------
    // UI Helpers: Badges
    // --------------------------------------------------------------------
    const severityBadge = (sev?: string) => {
        const s = (sev || "").toLowerCase();
        if (s === "critical")
            return (<span className="px-2 py-1 bg-red-600 text-white text-xs rounded font-bold">🔥 Critical</span>);
        if (s === "warning")
            return (<span className="px-2 py-1 bg-orange-500 text-white text-xs rounded font-bold">⚠ Warning</span>);
        return (<span className="px-2 py-1 bg-blue-600 text-white text-xs rounded">Info</span>);
    };

    const osBadge = (os?: string) => {
        const o = (os || "").toLowerCase();
        if (o === "windows")
            return (<span className="px-2 py-1 bg-blue-700 text-white text-xs rounded">🪟 Windows</span>);
        if (o === "linux")
            return (<span className="px-2 py-1 bg-yellow-600 text-white text-xs rounded">🐧 Linux</span>);
        return (<span className="px-2 py-1 bg-gray-500 text-white text-xs rounded">Unknown</span>);
    };

    const typeBadge = (type: string) => {
        const t = type.toLowerCase();
        if (t.includes("burst"))
            return (<span className="px-2 py-1 bg-red-700 text-white text-xs rounded font-bold">🔥 {type}</span>);
        if (t.includes("login"))
            return (<span className="px-2 py-1 bg-orange-600 text-white text-xs rounded font-bold">⚠ {type}</span>);
        return (<span className="px-2 py-1 bg-blue-700 text-white text-xs rounded">{type}</span>);
    };

    const channelBadge = (c: string) => {
        if (c.toLowerCase() === "sms")
            return <span className="px-2 py-1 bg-green-600 text-white text-xs rounded">📱 SMS</span>;
        return <span className="px-2 py-1 bg-purple-600 text-white text-xs rounded">✉ Email</span>;
    };

    // --------------------------------------------------------------------
    // Pagination buttons generator
    // --------------------------------------------------------------------
    const getPageNumbers = () => {
        const pages = [];

        if (totalPages <= 7) {
            for (let i = 1; i <= totalPages; i++) pages.push(i);
            return pages;
        }

        pages.push(1);
        if (page > 3) pages.push("...");

        for (let i = page - 1; i <= page + 1; i++) {
            if (i > 1 && i < totalPages) pages.push(i);
        }

        if (page < totalPages - 2) pages.push("...");
        pages.push(totalPages);

        return pages;
    };

    // --------------------------------------------------------------------
    // UI
    // --------------------------------------------------------------------
    return (
        <div>
            {/* HEADER */}
            <div className="flex justify-between items-center mb-4">
                <h2 className="text-xl font-semibold">Alert History</h2>

                <button
                    onClick={fetchAlerts}
                    disabled={loading}
                    className="text-sm px-3 py-1 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50 flex items-center gap-2"
                >
                    🔄 Refresh Now
                </button>
            </div>

            {/* FILTER BAR */}
            <div className="flex flex-wrap items-end gap-4 mb-6 bg-gray-100 p-4 rounded-lg shadow-inner">

                {/* Search */}
                <div className="flex flex-col">
                    <label className="text-sm font-semibold text-gray-700">Search</label>
                    <input
                        type="text"
                        className="px-3 py-2 rounded-md border shadow-sm focus:ring focus:ring-blue-200"
                        placeholder="Search alerts…"
                        value={search}
                        onChange={(e) => {
                            setPage(1);
                            setSearch(e.target.value);
                        }}
                    />
                </div>

                {/* Start Date */}
                <div className="flex flex-col">
                    <label className="text-sm font-semibold text-gray-700">Start Date</label>
                    <input
                        type="date"
                        className="px-3 py-2 rounded-md border"
                        value={startDate}
                        onChange={(e) => {
                            setPage(1);
                            setStartDate(e.target.value);
                        }}
                    />
                </div>

                {/* End Date */}
                <div className="flex flex-col">
                    <label className="text-sm font-semibold text-gray-700">End Date</label>
                    <input
                        type="date"
                        className="px-3 py-2 rounded-md border"
                        value={endDate}
                        onChange={(e) => {
                            setPage(1);
                            setEndDate(e.target.value);
                        }}
                    />
                </div>

                {/* Sort By */}
                <div className="flex flex-col">
                    <label className="text-sm font-semibold text-gray-700">Sort By</label>
                    <select
                        className="px-3 py-2 rounded-md border"
                        value={sortBy}
                        onChange={(e) => {
                            setPage(1);
                            setSortBy(e.target.value);
                        }}
                    >
                        <option value="timestamp">Timestamp</option>
                        <option value="type">Type</option>
                        <option value="channel">Channel</option>
                        <option value="severity">Severity</option>
                    </select>
                </div>
            </div>

            {/* TABLE */}
            <div className="overflow-x-auto">
                <table className="min-w-full bg-white rounded-lg shadow-md text-sm">
                    <thead className="bg-blue-600 text-white uppercase text-xs">
                        <tr>
                            <th className="px-4 py-2 text-left">Timestamp</th>
                            <th className="px-4 py-2 text-left">Type</th>
                            <th className="px-4 py-2 text-left">Severity</th>
                            <th className="px-4 py-2 text-left">Channel</th>
                            <th className="px-4 py-2 text-left">OS</th>
                            <th className="px-4 py-2 text-left">User</th>
                            <th className="px-4 py-2 text-left">IP</th>
                            <th className="px-4 py-2 text-left">Location</th>
                            <th className="px-4 py-2 text-left">Message</th>
                        </tr>
                    </thead>

                    <tbody>
                        {alerts.map((a) => (
                            <tr key={a.id} className="border-b hover:bg-blue-50 transition">
                                <td className="px-4 py-2">{new Date(a.timestamp).toLocaleString()}</td>
                                <td className="px-4 py-2">{typeBadge(a.type)}</td>
                                <td className="px-4 py-2">{severityBadge(a.severity)}</td>
                                <td className="px-4 py-2">{channelBadge(a.channel)}</td>
                                <td className="px-4 py-2">{osBadge(a.operatingSystem)}</td>
                                <td className="px-4 py-2">{a.username || "-"}</td>
                                <td className="px-4 py-2">{a.ipAddress || "-"}</td>
                                <td className="px-4 py-2">
                                    {a.country ? `${a.city}, ${a.country}` : "-"}
                                </td>
                                <td className="px-4 py-2">{a.message}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            {/* LAST UPDATED */}
            {lastUpdated && (
                <p className="text-xs text-gray-500 mt-2">
                    Last updated: {lastUpdated.toLocaleTimeString()}
                </p>
            )}

            {/* PAGINATION */}
            <div className="flex justify-center mt-6 space-x-2">
                <button
                    disabled={page === 1}
                    onClick={() => setPage(page - 1)}
                    className={`px-4 py-2 rounded-lg ${page === 1
                            ? "bg-gray-300 cursor-not-allowed"
                            : "bg-blue-600 text-white hover:bg-blue-700"
                        }`}
                >
                    Prev
                </button>

                {getPageNumbers().map((p, idx) => (
                    <button
                        key={idx}
                        disabled={p === "..."}
                        onClick={() => typeof p === "number" && setPage(p)}
                        className={`px-3 py-2 rounded-lg ${page === p
                                ? "bg-blue-600 text-white"
                                : "bg-gray-200 hover:bg-gray-300"
                            }`}
                    >
                        {p}
                    </button>
                ))}

                <button
                    disabled={page === totalPages}
                    onClick={() => setPage(page + 1)}
                    className={`px-4 py-2 rounded-lg ${page === totalPages
                            ? "bg-gray-300 cursor-not-allowed"
                            : "bg-blue-600 text-white hover:bg-blue-700"
                        }`}
                >
                    Next
                </button>
            </div>
        </div>
    );
}
