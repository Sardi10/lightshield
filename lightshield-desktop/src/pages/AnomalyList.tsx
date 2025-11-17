import { useEffect, useState } from "react";

export default function AnomalyList() {
    const [anomalies, setAnomalies] = useState([]);

    // Filters
    const [search, setSearch] = useState("");
    const [startDate, setStartDate] = useState("");
    const [endDate, setEndDate] = useState("");
    const [sortBy, setSortBy] = useState("timestamp");

    // Pagination
    const [page, setPage] = useState(1);
    const pageSize = 5;
    const [totalCount, setTotalCount] = useState(0);
    const totalPages = Math.ceil(totalCount / pageSize);

    // Loading + Last Updated
    const [loading, setLoading] = useState(false);
    const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

    // Fetch anomalies
    const fetchAnomalies = async () => {
        setLoading(true);

        const params = new URLSearchParams({
            page: page.toString(),
            pageSize: pageSize.toString(),
            sortBy,
            sortDir: "desc" // always newest first
        });

        if (search) params.append("search", search);
        if (startDate) params.append("startDate", startDate);
        if (endDate) params.append("endDate", endDate);

        try {
            const res = await fetch(
                `http://localhost:5213/api/anomalies?${params.toString()}`
            );
            const data = await res.json();

            setAnomalies(data.items);
            setTotalCount(data.totalCount);
        } catch (err) {
            console.error("Failed to fetch anomalies:", err);
        } finally {
            setLoading(false);
            setLastUpdated(new Date());
        }
    };

    // Re-fetch when inputs change
    useEffect(() => {
        fetchAnomalies();
    }, [page, search, startDate, endDate, sortBy]);

    useEffect(() => {
        fetchAnomalies();
        const interval = setInterval(fetchAnomalies, 5000);
        return () => clearInterval(interval);
    }, [search, startDate, endDate, sortBy, page]);



    // Pagination numbers with ellipses
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

    return (
        <div>

            {/* ---------------- TITLE + REFRESH BUTTON ---------------- */}
            <div className="flex justify-between items-center mb-2">
                <h2 className="text-xl font-semibold">Anomaly Log</h2>

                <button
                    onClick={fetchAnomalies}
                    disabled={loading}
                    className="text-sm px-3 py-1 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50 flex items-center gap-2"
                >
                    <span className="text-lg">🔄</span>
                    Refresh Now
                </button>
            </div>

            {/* ---------------- FILTER BAR ---------------- */}
            <div className="flex flex-wrap items-end gap-4 mb-6 bg-gray-100 p-4 rounded-lg shadow-inner">

                {/* Search */}
                <div className="flex flex-col">
                    <label className="text-sm font-semibold text-gray-700">Search</label>
                    <input
                        type="text"
                        className="px-3 py-2 rounded-md border shadow-sm focus:ring focus:ring-blue-200"
                        placeholder="Search all fields…"
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
                        <option value="hostname">Hostname</option>
                        <option value="type">Type</option>
                    </select>
                </div>
            </div>

           

            {/* ---------------- TABLE ---------------- */}
            <div className="overflow-x-auto">
                <table className="min-w-full bg-white rounded-lg shadow-md">
                    <thead className="bg-blue-600 text-white text-sm uppercase">
                        <tr>
                            <th className="px-4 py-2 text-left">Timestamp</th>
                            <th className="px-4 py-2 text-left">Hostname</th>
                            <th className="px-4 py-2 text-left">Type</th>
                            <th className="px-4 py-2 text-left">Description</th>
                        </tr>
                    </thead>
                    <tbody>
                        {anomalies.map((a) => (
                            <tr key={a.id} className="border-b hover:bg-blue-50">
                                <td className="px-4 py-2">{new Date(a.timestamp).toLocaleString()}</td>
                                <td className="px-4 py-2">{a.hostname}</td>
                                <td className="px-4 py-2 font-semibold text-blue-700">{a.type}</td>
                                <td className="px-4 py-2">{a.description}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            {/* ---------------- LAST UPDATED ---------------- */}
            {lastUpdated && (
                <p className="text-xs text-gray-500 mt-2">
                    Last updated: {lastUpdated.toLocaleTimeString()}
                </p>
            )}

            {/* ---------------- PAGINATION ---------------- */}
            <div className="flex justify-center mt-6 space-x-2">

                <button
                    disabled={page === 1}
                    onClick={() => setPage(page - 1)}
                    className={`px-4 py-2 rounded-lg ${page === 1 ? "bg-gray-300 cursor-not-allowed" : "bg-blue-600 text-white hover:bg-blue-700"
                        }`}
                >
                    Prev
                </button>

                {getPageNumbers().map((p, i) => (
                    <button
                        key={i}
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
