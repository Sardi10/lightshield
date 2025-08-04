// src/pages/Dashboard.tsx
import { useEffect, useState } from 'react'
import type { Anomaly } from '../types'
import { Link } from 'react-router-dom'

export default function Dashboard() {
    const [anomalies, setAnomalies] = useState<Anomaly[]>([])
    const [loading, setLoading] = useState(true)

    useEffect(() => {
        fetch('http://localhost:5213/api/anomalies')
            .then(res => {
                if (!res.ok) throw new Error(`HTTP ${res.status}`)
                return res.json() as unknown
            })
            .then((json: unknown) => {
                let list: Anomaly[] = []
                // If it's already an array
                if (Array.isArray(json)) {
                    list = json as Anomaly[]
                }
                // Or if it's wrapped in { items: Anomaly[] }
                else if (
                    typeof json === 'object' &&
                    json !== null &&
                    'items' in json &&
                    Array.isArray((json as { items?: unknown }).items)
                ) {
                    list = (json as { items: Anomaly[] }).items
                }
                setAnomalies(list)
            })
            .catch(err => {
                console.error(err)
                setAnomalies([])
            })
            .finally(() => setLoading(false))
    }, [])

    return (
        <div className="p-8 max-w-2xl mx-auto">
            <h1 className="text-2xl font-semibold mb-4">Anomalies</h1>
            <Link to="/" className="text-blue-600 hover:underline mb-4 inline-block">
                ← Back to Onboarding
            </Link>
            {loading ? (
                <p>Loading…</p>
            ) : anomalies.length === 0 ? (
                <p>No anomalies found.</p>
            ) : (
                <ul className="list-disc pl-5 space-y-2">
                    {anomalies.map(a => (
                        <li key={a.id}>
                            <strong>{a.type}</strong> on <em>{a.hostname}</em> at{' '}
                            {new Date(a.timestamp).toLocaleString()}
                        </li>
                    ))}
                </ul>
            )}
        </div>
    )
}
