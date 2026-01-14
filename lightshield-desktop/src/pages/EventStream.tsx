import { useEffect, useState } from "react";

// --------------------------------------------------------------------
// FIX: Normalize timestamps before Date parsing
// --------------------------------------------------------------------
const normalizeTimestamp = (ts: string) => {
    if (!ts) return ts;
    return ts.replace(/\.\d+Z$/, "Z"); // strip microseconds
};

export default function EventStream() {
    const [events, setEvents] = useState([]);

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

    // UI state
    const [loading, setLoading] = useState(false);
    const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

    // --------------------------------------------------------------------
    // Fetch events
    // --------------------------------------------------------------------
    const fetchEvents = async () => {
        setLoading(true);

        const params = new URLSearchParams({
            page: page.toString(),
            pageSize: pageSize.toString(),
            sortBy,
            sortDir: "desc",
        });

        if (search) params.append("search", search);
        if (startDate) params.append("startDate", startDate);
        if (endDate) params.append("endDate", endDate);

        try {
            const res = await fetch(
                `http://localhost:5213/api/events?${params.toString()}`
            );
            const data = await res.json();

            // SAFE pagination update
            const newTotalPages = Math.ceil(data.totalCount / pageSize);
            if (page > newTotalPages && newTotalPages > 0) {
                setPage(newTotalPages);
                setLoading(false);
                setLastUpdated(new Date());
                return;
            }

            // FIX: Normalize timestamps and store items
            const normalized = data.items.map((e) => ({
                ...e,
                Timestamp: normalizeTimestamp(e.Timestamp),
            }));

            setEvents(normalized);
            setTotalCount(data.totalCount);

        } catch (err) {
            console.error("Failed to fetch events:", err);
        } finally {
            setLoading(false);
            setLastUpdated(new Date());
        }
    };

    useEffect(() => {
        fetchEvents();
    }, [page, search, startDate, endDate, sortBy]);

    useEffect(() => {
        const interval = setInterval(fetchEvents, 5000);
        return () => clearInterval(interval);
    }, [page, search, startDate, endDate, sortBy]);

    // --------------------------------------------------------------------
    // Severity badge
    // --------------------------------------------------------------------
    const severityBadge = (sev) => {
        const s = (sev || "").toLowerCase();

        if (s === "critical")
            return (
                <span className="inline-flex items-center gap-1 px-2 py-1 bg-red-600 text-white text-xs font-bold rounded whitespace-nowrap">
                    🔥 Critical
                </span>
            );

        if (s === "warning")
            return (
                <span className="inline-flex items-center gap-1 px-2 py-1 bg-orange-500 text-white text-xs font-bold rounded whitespace-nowrap">
                    ⚠ Warning
                </span>
            );

        return (
            <span className="inline-flex items-center gap-1 px-2 py-1 bg-blue-600 text-white text-xs font-bold rounded whitespace-nowrap">
                Info
            </span>
        );
    };

    // --------------------------------------------------------------------
    // OS badge
    // --------------------------------------------------------------------
    const osBadge = (os) => {
        const o = (os || "").toLowerCase();

        if (o === "windows")
            return (
                <span className="inline-flex items-center gap-1 px-2 py-1 bg-blue-700 text-white text-xs rounded whitespace-nowrap">
                    🪟 Windows
                </span>
            );

        if (o === "linux")
            return (
                <span className="inline-flex items-center gap-1 px-2 py-1 bg-yellow-600 text-white text-xs rounded whitespace-nowrap">
                    🐧 Linux
                </span>
            );

        return (
            <span className="inline-flex items-center gap-1 px-2 py-1 bg-gray-500 text-white text-xs rounded whitespace-nowrap">
               ❓ Unknown
            </span>
        );
    };

    // --------------------------------------------------------------------
    // Pagination numbers
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
            <div className="flex justify-between items-center mb-3">
                <h2 className="text-xl font-semibold text-gray-800">
                    Live Event Stream
                </h2>

                <button
                    onClick={fetchEvents}
                    disabled={loading}
                    className="text-sm px-3 py-1 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50 flex items-center gap-2"
                >
                    🔄 Refresh Now
                </button>
            </div>

            {/* FILTER BAR */}
            <div className="flex flex-wrap items-end gap-4 mb-6 bg-gray-100 p-4 rounded-lg shadow-inner">

                {/* SEARCH */}
                <div className="flex flex-col">
                    <label className="text-sm font-semibold text-gray-700">
                        Search
                    </label>
                    <input
                        type="text"
                        className="px-3 py-2 rounded-md border shadow-sm"
                        placeholder="Search all fields…"
                        value={search}
                        onChange={(e) => {
                            setPage(1);
                            setSearch(e.target.value);
                        }}
                    />
                </div>

                {/* START DATE */}
                <div className="flex flex-col">
                    <label className="text-sm font-semibold text-gray-700">
                        Start Date
                    </label>
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

                {/* END DATE */}
                <div className="flex flex-col">
                    <label className="text-sm font-semibold text-gray-700">
                        End Date
                    </label>
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

                {/* SORT */}
                <div className="flex flex-col">
                    <label className="text-sm font-semibold text-gray-700">
                        Sort By
                    </label>
                    <select
                        className="px-3 py-2 rounded-md border"
                        value={sortBy}
                        onChange={(e) => {
                            setPage(1);
                            setSortBy(e.target.value);
                        }}
                    >
                        <option value="timestamp">Timestamp</option>
                        <option value="hostname">Hostname</option>
                        <option value="type">Type</option>
                        <option value="source">Source</option>
                        <option value="message">Message</option>
                        <option value="severity">Severity</option>
                        <option value="username">Username</option>
                        <option value="ipaddress">IP</option>
                        <option value="operatingsystem">OS</option>
                    </select>
                </div>
            </div>

            {/* TABLE */}
            <div className="overflow-x-auto">
                <table className="min-w-full bg-white rounded-lg shadow-md">
                    <thead className="bg-blue-600 text-white text-sm uppercase">
                        <tr>
                            <th className="px-4 py-2 text-left">Timestamp</th>
                            <th className="px-4 py-2 text-left">Hostname</th>
                            <th className="px-4 py-2 text-left">Type</th>
                            <th className="px-4 py-2 text-left">Severity</th>
                            <th className="px-4 py-2 text-left">OS</th>
                            <th className="px-4 py-2 text-left">User</th>
                            <th className="px-4 py-2 text-left">IP</th>
                            <th className="px-4 py-2 text-left">Location</th>
                            <th className="px-4 py-2 text-left">Message</th>
                        </tr>
                    </thead>

                    <tbody>
                        {events.map((e) => (
                            <tr key={e.Id} className="border-b hover:bg-blue-50 transition">
                                <td className="px-4 py-2">
                                    {new Date(e.Timestamp).toLocaleString()}
                                </td>
                                <td className="px-4 py-2">{e.Hostname}</td>
                                <td className="px-4 py-2">{e.Type}</td>
                                <td className="px-4 py-2 align-middle">{severityBadge(e.Severity)}</td>
                                <td className="px-4 py-2 align-middle">{osBadge(e.OperatingSystem)}</td>
                                <td className="px-4 py-2">{e.Username || "-"}</td>
                                <td className="px-4 py-2">{e.IPAddress || "-"}</td>
                                <td className="px-4 py-2">
                                    {e.Country ? `${e.City}, ${e.Country}` : "-"}
                                </td>
                                <td className="px-4 py-2">{e.Message}</td>
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

                {getPageNumbers().map((p, i) => (
                    <button
                        key={i}
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
