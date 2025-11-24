import { useEffect, useState } from "react";

interface AlertLogEntry {
    Id: number;
    Timestamp: string;
    Type: string;
    Message: string;
    Channel: string;
    Hostname: string;
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

            // Prevent resetting page to 1 when data shrinks
            if (page > newTotalPages && newTotalPages > 0) {
                setPage(newTotalPages);
                setLastUpdated(new Date());
                setLoading(false);
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

    useEffect(() => {
        fetchAlerts();
    }, [page, search, startDate, endDate, sortBy]);

    // Auto-refresh every 10 seconds
    useEffect(() => {
        const interval = setInterval(fetchAlerts, 10000);
        return () => clearInterval(interval);
    }, [page, search, startDate, endDate, sortBy]);

    // Page number generator
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
                        className="px-3 py-2 rounded-md border shadow-sm"
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
                        <option value="hostname">Hostname</option>
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
                            <th className="px-4 py-2 text-left">Channel</th>
                            <th className="px-4 py-2 text-left">Hostname</th>
                            <th className="px-4 py-2 text-left">Message</th>
                        </tr>
                    </thead>

                    <tbody>
                        {alerts.map((a) => (
                            <tr key={a.Id} className="border-b hover:bg-blue-50 transition">
                                <td className="px-4 py-2">
                                    {new Date(a.Timestamp).toLocaleString()}
                                </td>
                                <td className="px-4 py-2">{a.Type}</td>
                                <td className="px-4 py-2">{a.Channel}</td>
                                <td className="px-4 py-2">{a.Hostname}</td>
                                <td className="px-4 py-2">{a.Message}</td>
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
                    className={`px-4 py-2 rounded-lg ${page === 1 ? "bg-gray-300 cursor-not-allowed" : "bg-blue-600 text-white hover:bg-blue-700"
                        }`}
                >
                    Prev
                </button>

                {getPageNumbers().map((p, idx) => (
                    <button
                        key={idx}
                        disabled={p === "..."}
                        onClick={() => typeof p === "number" && setPage(p)}
                        className={`px-3 py-2 rounded-lg ${page === p ? "bg-blue-600 text-white" : "bg-gray-200 hover:bg-gray-300"
                            }`}
                    >
                        {p}
                    </button>
                ))}

                <button
                    disabled={page === totalPages}
                    onClick={() => setPage(page + 1)}
                    className={`px-4 py-2 rounded-lg ${page === totalPages ? "bg-gray-300 cursor-not-allowed" : "bg-blue-600 text-white hover:bg-blue-700"
                        }`}
                >
                    Next
                </button>
            </div>
        </div>
    );
}
